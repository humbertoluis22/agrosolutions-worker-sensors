using AgrosolutionsWorkerSensors.Generator;
using AgrosolutionsWorkerSensors.Generator.Service;
using System.Net.Http;
using System.Net.Http.Json;


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.AddHttpClient("ApiRaw", client =>
{
    client.BaseAddress = new Uri("https://localhost:5001"); // URL da sua API
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHostedService<DataGenerationWorker>();

var host = builder.Build();


host.Run();
