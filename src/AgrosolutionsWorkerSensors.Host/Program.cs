
using AgrosolutionsWorkerSensors.Host;
using AgrosolutionsWorkerSensors.Infrastructure.Data;
using AgrosolutionsWorkerSensors.Registration.Services;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

using StackExchange.Redis;


var builder = Host.CreateApplicationBuilder(args);
// PostgreSQL
builder.Services.AddDbContext<SensorContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));


// Registrar o Service que usará a conexão
builder.Services.AddHostedService<RegistrationWorker>();


var host = builder.Build();
host.Run();
