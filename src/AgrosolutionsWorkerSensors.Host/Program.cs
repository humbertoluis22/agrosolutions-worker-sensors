
using AgrosolutionsWorkerSensors.Host;
using AgrosolutionsWorkerSensors.Infrastructure.Data;
using AgrosolutionsWorkerSensors.Registration.Services;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

using StackExchange.Redis;


var builder = Host.CreateApplicationBuilder(args);
// PostgreSQL
builder.Services.AddDbContext<SensorContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


// Registrar o Service que usar� a conex�o
builder.Services.AddHostedService<RegistrationWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SensorContext>();
    db.Database.Migrate();
}

host.Run();
