using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Catalog.Storage;

#nullable disable

namespace Catalog.Storage.Migrations
{
    /// <summary>BE-084: измерение владельца позиции каталога. Старые строки — ClientId = NULL (не отдаются никому до backfill).</summary>
    [DbContext(typeof(CommandDbContext))]
    [Migration("20261106150000_AddClientIdToCatalogItem")]
    public partial class AddClientIdToCatalogItem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(name: "ClientId", table: "catalog_items", type: "uuid", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_catalog_items_ClientId", table: "catalog_items", column: "ClientId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_catalog_items_ClientId", table: "catalog_items");
            migrationBuilder.DropColumn(name: "ClientId", table: "catalog_items");
        }
    }
}
