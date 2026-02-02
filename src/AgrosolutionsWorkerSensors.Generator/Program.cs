using AgrosolutionsWorkerSensors.Generator;
using AgrosolutionsWorkerSensors.Generator.Service;
using AgrosolutionsWorkerSensors.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Net.Http.Json;


var builder = Host.CreateApplicationBuilder(args);

// 1. Configurar o Banco de Dados
// Ele precisa ler o banco para saber quais sensores existem
builder.Services.AddDbContext<SensorContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// 2. Configurar o HttpClient
// Cria um cliente HTTP nomeado "ApiRaw" já com o endereço base configurado
builder.Services.AddHttpClient("ApiRaw", client =>
{
    // Lê a URL do appsettings.json ou usa localhost:5000 como fallback
    string url = builder.Configuration["ApiRawUrl"] ?? "http://localhost:5198";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddHttpClient("ApiRaw", client =>
{
    client.BaseAddress = new Uri("http://localhost:5198"); // URL da sua API
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHostedService<DataGenerationWorker>();

var host = builder.Build();


host.Run();
