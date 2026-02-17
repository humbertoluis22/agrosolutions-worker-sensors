
using AgrosolutionsWorkerSensors.Host;
using AgrosolutionsWorkerSensors.Infrastructure.Data;
using AgrosolutionsWorkerSensors.Registration.Services;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Amazon;

using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);
// PostgreSQL
builder.Services.AddDbContext<SensorContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


// Registrar o Service que usar� a conex�o
var awsRegion = builder.Configuration["AWS:Region"];

builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    return new AmazonSQSClient(RegionEndpoint.GetBySystemName(awsRegion));
});

builder.Services.AddHostedService<RegistrationWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SensorContext>();
    db.Database.Migrate();
}

host.Run();
