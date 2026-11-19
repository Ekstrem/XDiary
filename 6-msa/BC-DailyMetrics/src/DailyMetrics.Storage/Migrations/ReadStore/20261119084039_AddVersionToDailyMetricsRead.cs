using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyMetrics.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class AddVersionToDailyMetricsRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BE-140: таблица daily_metrics_read уже создана миграцией AddReadStores —
            // добавляем только Version (stale-skip QG-305), таблица не пересоздаётся.
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "daily_metrics_read",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "daily_metrics_read");
        }
    }
}
