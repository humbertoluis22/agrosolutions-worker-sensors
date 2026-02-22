using AgrosolutionsWorkerSensors.Domain.Entities;
using AgrosolutionsWorkerSensors.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgrosolutionsWorkerSensors.Infrastructure.Data
{
    public class SensorContext : DbContext
    {
        public SensorContext(DbContextOptions<SensorContext> options) : base(options) { }

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