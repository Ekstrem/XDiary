using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class AddVersionToCatalogRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BE-138: таблица catalog_read уже создана миграцией AddReadStores —
            // добавляем только Version (stale-skip QG-305) и недостающий индекс по ClientId.
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "catalog_read",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_read_ClientId",
                table: "catalog_read",
                column: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_catalog_read_ClientId",
                table: "catalog_read");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "catalog_read");
        }
    }
}
