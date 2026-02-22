using AgrosolutionsWorkerSensors.Domain.Entities;
using AgrosolutionsWorkerSensors.Domain.Enums;
using AgrosolutionsWorkerSensors.Infrastructure.Data;
using AgrosolutionsWorkerSensors.Registration.Dtos; 
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgrosolutionsWorkerSensors.Registration.Services;

public class RegistrationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RegistrationWorker> _logger;
    private readonly IAmazonSQS _sqsClient;
    private readonly string _queueUrl;

    public RegistrationWorker(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IAmazonSQS sqsClient, 
        ILogger<RegistrationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        _sqsClient = sqsClient; 
        
        _queueUrl = configuration["AWS:SqsQueueUrl"] 
                    ?? throw new ArgumentNullException("AWS:SqsQueueUrl não configurado.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando consumidor SQS para Registro de Sensores...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Configuração da requisição para o SQS
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = 1, // Processa uma por vez para garantir consistência
                    WaitTimeSeconds = 20 // Long Polling: aguarda até 20s se a fila estiver vazia (reduz custos)
                };

                var response = await _sqsClient.ReceiveMessageAsync(request, stoppingToken);

                if (response?.Messages == null || !response.Messages.Any())
                {
                    _logger.LogError("Mensagem SQS esta vazia");
                    continue;
                }

                
                foreach (var message in response.Messages)
                {
                    try
                    {
                        // 1. Lê o Body inicial
                        string payloadJson = message.Body;

                        using var outerDocument = JsonDocument.Parse(payloadJson);
                        var outerRoot = outerDocument.RootElement;

                        // 2. Verifica se é um envelope do AWS SNS
                        if (outerRoot.TryGetProperty("Type", out var typeElement) && typeElement.GetString() == "Notification")
                        {
                            // Se for SNS, o payload real está dentro da propriedade "Message" como uma string
                            if (outerRoot.TryGetProperty("Message", out var snsMessageProp))
                            {
                                payloadJson = snsMessageProp.GetString(); // Atualiza o payload com o conteúdo interno
                            }
                        }

                        // 3. Agora fazemos o parse do envelope do MassTransit
                        using var mtDocument = JsonDocument.Parse(payloadJson);
                        var mtRoot = mtDocument.RootElement;

                        // 4. Extrai o "message" minúsculo (onde está o seu DTO)
                        if (mtRoot.TryGetProperty("message", out var mtMessageElement))
                        {
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var sensorDto = JsonSerializer.Deserialize<RegisterSensorMessage>(mtMessageElement.GetRawText(), options);

                            if (sensorDto != null)
                            {
                                // Executa a lógica
                                await ProcessSensorAsync(sensorDto);

                                // Remove a mensagem
                                await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, stoppingToken);
                                _logger.LogInformation("Mensagem ID {MessageId} processada com sucesso.", message.MessageId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("O envelope (MassTransit) da mensagem ID {MessageId} não contém a propriedade 'message'.", message.MessageId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao processar mensagem individual ID {MessageId}", message.MessageId);
                    }
                }
            }
            catch (Exception ex)
            {
                // Erro de conexão com o SQS ou algo infraestrutural
                _logger.LogError(ex, "Erro na conexão ou recebimento da fila SQS.");
                await Task.Delay(5000, stoppingToken); // Backoff de 5s antes de tentar reconectar
            }
        }
    }

    private async Task ProcessSensorAsync(RegisterSensorMessage dto)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SensorContext>();

        var existingSensor = await dbContext.Sensors.FindAsync(dto.SensorId);

        if (dto.TypeOperation == TypeOperation.Create)
        {
            if (existingSensor == null)
            {
                var newSensor = new SensorRaw
                {
                    SensorId = dto.SensorId,
                    FieldId = dto.FieldId,
                    DtCreated = dto.DtCreated,
                    TypeSensor = dto.TypeSensor,
                    StatusSensor = dto.StatusSensor,
                    TypeOperation = dto.TypeOperation
                };
                dbContext.Sensors.Add(newSensor);
                _logger.LogInformation($"Sensor {dto.SensorId} criado.");
            }
            else
            {
                // Regra: Se existe, valida status
                existingSensor.StatusSensor = dto.StatusSensor;
                dbContext.Sensors.Update(existingSensor);
                _logger.LogInformation($"Sensor {dto.StatusSensor} já existia. Status atualizado.");
            }
        }
        else if (dto.TypeOperation == TypeOperation.Update)
        {
            if (existingSensor != null)
            {
                existingSensor.SensorId = dto.SensorId;
                existingSensor.FieldId = dto.FieldId;
                existingSensor.DtCreated = dto.DtCreated;
                existingSensor.TypeSensor = dto.TypeSensor;
                existingSensor.StatusSensor = dto.StatusSensor;
                existingSensor.TypeOperation = dto.TypeOperation;
                dbContext.Sensors.Update(existingSensor);
            }
        }
        else if (dto.TypeOperation == TypeOperation.Delete)
        {
            if (existingSensor != null)
            {
                dbContext.Sensors.Remove(existingSensor);
                _logger.LogInformation($"Sensor {dto.SensorId} removido.");
            }
        }

        await dbContext.SaveChangesAsync();
    }
}