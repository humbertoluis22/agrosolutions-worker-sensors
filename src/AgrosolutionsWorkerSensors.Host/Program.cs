
using AgrosolutionsWorkerSensors.Host;
using AgrosolutionsWorkerSensors.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

using StackExchange.Redis;


var builder = Host.CreateApplicationBuilder(args);
// PostgreSQL
builder.Services.AddDbContext<SensorContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));


//// Redis
//builder.Services.AddSingleton<IConnectionMultiplexer>(
//    _ => ConnectionMultiplexer.Connect("localhost:6379"));


// RabbitMQ
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    return new ConnectionFactory { HostName = "localhost" };
});


// Registrar o Service que usará a conexão
builder.Services.AddHostedService<Worker>();


var host = builder.Build();
host.Run();
