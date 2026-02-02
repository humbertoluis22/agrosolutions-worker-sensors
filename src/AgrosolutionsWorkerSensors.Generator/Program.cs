using AgrosolutionsWorkerSensors.Generator;
using AgrosolutionsWorkerSensors.Generator.Service;
using AgrosolutionsWorkerSensors.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Net.Http.Json;


var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<SensorContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("ApiRaw", client =>
{
    string url = builder.Configuration["ApiRawUrl"] ?? "http://localhost:5198";
    client.BaseAddress = new Uri(url); // URL da sua API
    int intervalSecond = builder.Configuration
    .GetValue<int>("GenerationSettings:IntervalSeconds", 16);
    client.Timeout = TimeSpan.FromSeconds(intervalSecond);
});

builder.Services.AddHostedService<DataGenerationWorker>();

var host = builder.Build();


host.Run();
