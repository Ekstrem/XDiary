using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyMetrics.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Stress = table.Column<int>(type: "integer", nullable: false),
                    RestingHr = table.Column<int>(type: "integer", nullable: false),
                    Hrv = table.Column<int>(type: "integer", nullable: false),
                    SleepScore = table.Column<int>(type: "integer", nullable: false),
                    SleepDurationMin = table.Column<int>(type: "integer", nullable: false),
                    SleepDeepMin = table.Column<int>(type: "integer", nullable: false),
                    SleepLightMin = table.Column<int>(type: "integer", nullable: false),
                    SleepRemMin = table.Column<int>(type: "integer", nullable: false),
                    SleepAwakeMin = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_metrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_metrics_Date",
                table: "daily_metrics",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_metrics");
        }
    }
}
