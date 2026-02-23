using System.Net.Http.Json;
using AgrosolutionsWorkerSensors.Domain.Enums;
using AgrosolutionsWorkerSensors.Generator.Dtos.SensorData;
using AgrosolutionsWorkerSensors.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgrosolutionsWorkerSensors.Generator.Service
{
    public class DataGenerationWorker(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<DataGenerationWorker> logger
        ) : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<DataGenerationWorker> _logger = logger;
        private readonly Random _random = new();
        private readonly int _intervalSeconds = config.GetValue<int>("GenerationSettings:IntervalSeconds", 5);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Iniciando Worker de Geração de Dados. Intervalo: {Interval}s.",
                _intervalSeconds
            );

            var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await GenerateAndSendMetricsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no ciclo de geração de dados.");
                }
            }
        }

        private async Task GenerateAndSendMetricsAsync(CancellationToken token)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SensorContext>();

            // Busca sensores ativos (para não estourar memória, em prod use paginação)
            var sensors = await dbContext.Sensors.Where(s => s.StatusSensor).ToListAsync(token);

            if (!sensors.Any())
            {
                _logger.LogInformation("Nenhum sensor ativo encontrado. Aguardando próximo ciclo.");
                return;
            }

            _logger.LogInformation(
                "Iniciando ciclo de geração. {Count} sensor(es) ativo(s) encontrado(s).",
                sensors.Count
            );

            var client = _httpClientFactory.CreateClient("ApiRaw");
            int success = 0;
            int failures = 0;

            foreach (var sensor in sensors)
            {
                object dataPayload = GenerateDummyData(sensor.TypeSensor);

                var request = new
                {
                    FieldId = sensor.FieldId,
                    SensorId = sensor.SensorId,
                    Data = dataPayload,
                    TypeSensor = sensor.TypeSensor,
                    TimeStamp = DateTime.UtcNow,
                };

                var response = await client.PostAsJsonAsync(
                    "/api/ingestion/sensor",
                    request,
                    token
                );

                if (response.IsSuccessStatusCode)
                {
                    success++;
                    _logger.LogDebug(
                        "Dados enviados com sucesso para o sensor {SensorId}.",
                        sensor.SensorId
                    );
                }
                else
                {
                    failures++;
                    _logger.LogWarning(
                        "Falha ao enviar dados do sensor {SensorId}. Status: {StatusCode}.",
                        sensor.SensorId,
                        response.StatusCode
                    );
                }
            }

            _logger.LogInformation(
                "Ciclo concluído. Sucesso: {Success} | Falhas: {Failures}.",
                success,
                failures
            );
        }

        private object GenerateDummyData(SensorType type)
        {
            return type switch
            {
                SensorType.Solo => new SoloData(
                    Umidade: Math.Round(_random.NextDouble() * 100, 2),
                    Ph: Math.Round(_random.NextDouble() * 14, 1),
                    NutrientesData: new NutrientesData(
                        Nitrogenio: Math.Round(_random.NextDouble() * 50, 2),
                        Fosforo: Math.Round(_random.NextDouble() * 50, 2),
                        Potassio: Math.Round(_random.NextDouble() * 50, 2)
                    )
                ),
                SensorType.Silos => new SiloData(
                    NivelPreenchimento: Math.Round(_random.NextDouble() * 100, 2),
                    TemperaturaMedia: Math.Round(20 + _random.NextDouble() * 15, 2),
                    Co2: Math.Round(_random.NextDouble() * 500, 2)
                ),
                SensorType.Meteoroligica => new MeteorologicaData(
                    Temperatura: Math.Round(15 + _random.NextDouble() * 25, 2),
                    Umidade: Math.Round(_random.NextDouble() * 100, 2),
                    VelocidadeVento: Math.Round(_random.NextDouble() * 100, 2),
                    DirecaoVento: new[] { "N", "S", "L", "O", "NE" }[_random.Next(0, 4)],
                    ChuvaUltimaHora: Math.Round(_random.NextDouble() * 50, 2),
                    PontoOrvalho: Math.Round(10 + _random.NextDouble() * 10, 2)
                ),
                _ => new { },
            };
        }
    }
}
