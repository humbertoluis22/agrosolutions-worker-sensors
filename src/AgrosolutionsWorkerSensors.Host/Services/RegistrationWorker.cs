using AgrosolutionsWorkerSensors.Domain.Entities;
using AgrosolutionsWorkerSensors.Domain.Enums;
using AgrosolutionsWorkerSensors.Infrastructure.Data;
using AgrosolutionsWorkerSensors.Registration.Dtos;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;


namespace AgrosolutionsWorkerSensors.Registration.Services;
public class RegistrationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RegistrationWorker> _logger;
    private IConnection _connection;
    private IModel _channel;

    public RegistrationWorker(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<RegistrationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        InitRabbitMQ();
    }

    private void InitRabbitMQ()
    {
        var factory = new ConnectionFactory { HostName = _configuration["RabbitMQ:Host"] ?? "localhost" };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(queue: "register_sensor", durable: true, exclusive: false, autoDelete: false, arguments: null);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var sensorDto = JsonSerializer.Deserialize<RegisterSensorMessage>(message, options);

                if (sensorDto != null)
                {
                    await ProcessSensorAsync(sensorDto);
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem");
                // Em produção: enviar para Dead Letter Queue
                _channel.BasicNack(ea.DeliveryTag, false, false);
            }
        };

        _channel.BasicConsume(queue: "register_sensor", autoAck: false, consumer: consumer);
        return Task.CompletedTask;
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