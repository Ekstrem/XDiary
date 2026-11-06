using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using DailyMetrics.Storage;

#nullable disable

namespace DailyMetrics.Storage.Migrations
{
    /// <summary>BE-084: измерение владельца метрики. Старые строки — ClientId = NULL (не отдаются никому).</summary>
    [DbContext(typeof(CommandDbContext))]
    [Migration("20261106140000_AddClientIdToDailyMetrics")]
    public partial class AddClientIdToDailyMetrics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_daily_metrics_Date", table: "daily_metrics");
            migrationBuilder.AddColumn<Guid>(name: "ClientId", table: "daily_metrics", type: "uuid", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_daily_metrics_ClientId_Date", table: "daily_metrics", columns: new[] { "ClientId", "Date" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_daily_metrics_ClientId_Date", table: "daily_metrics");
            migrationBuilder.DropColumn(name: "ClientId", table: "daily_metrics");
            migrationBuilder.CreateIndex(name: "IX_daily_metrics_Date", table: "daily_metrics", column: "Date", unique: true);
        }
    }
}
