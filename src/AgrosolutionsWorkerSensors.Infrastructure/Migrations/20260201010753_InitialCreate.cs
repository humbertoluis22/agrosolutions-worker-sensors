using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgrosolutionsWorkerSensors.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sensors",
                columns: table => new
                {
                    SensorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    DtCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TypeSensor = table.Column<string>(type: "text", nullable: false),
                    StatusSensor = table.Column<bool>(type: "boolean", nullable: false),
                    TypeOperation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sensors", x => x.SensorId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sensors");
        }
    }
}
