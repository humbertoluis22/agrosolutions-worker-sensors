using AgrosolutionsWorkerSensors.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgrosolutionsWorkerSensors.Infrastructure.Data
{
    public class SensorContext(DbContextOptions<SensorContext> options) : DbContext(options)
    {
        public DbSet<SensorRaw> Sensors { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SensorRaw>(entity =>
            {
                entity.HasKey(e => e.SensorId);
                entity.Property(e => e.FieldId).IsRequired();
                entity.Property(e => e.TypeSensor).HasConversion<string>(); // Salvar enum como string para legibilidade
                entity.Property(e => e.TypeOperation).HasConversion<string>();
            });
        }
    }
}
