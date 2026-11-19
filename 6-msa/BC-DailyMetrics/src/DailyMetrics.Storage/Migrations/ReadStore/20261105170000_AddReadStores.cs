using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyMetrics.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    [DbContext(typeof(DailyMetrics.Storage.ReadDbContext))]
    [Migration("20261105170000_AddReadStores")]
    public partial class AddReadStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_metrics_read",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    Stress = table.Column<int>(type: "integer", nullable: false),
                    RestingHr = table.Column<int>(type: "integer", nullable: false),
                    Hrv = table.Column<int>(type: "integer", nullable: false),
                    SleepScore = table.Column<int>(type: "integer", nullable: false),
                    SleepDurationMin = table.Column<int>(type: "integer", nullable: false),
                    SleepDeepMin = table.Column<int>(type: "integer", nullable: false),
                    SleepLightMin = table.Column<int>(type: "integer", nullable: false),
                    SleepRemMin = table.Column<int>(type: "integer", nullable: false),
                    SleepAwakeMin = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_metrics_read", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "daily_metrics_read");
        }
    }
}
