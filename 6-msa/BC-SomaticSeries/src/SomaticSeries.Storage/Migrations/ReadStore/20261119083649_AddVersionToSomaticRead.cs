using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SomaticSeries.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class AddVersionToSomaticRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BE-140: таблица somatic_read уже создана миграцией AddReadStores —
            // добавляем Version (stale-skip QG-305) и недостающий индекс по ClientId.
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "somatic_read",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_somatic_read_ClientId",
                table: "somatic_read",
                column: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_somatic_read_ClientId",
                table: "somatic_read");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "somatic_read");
        }
    }
}
