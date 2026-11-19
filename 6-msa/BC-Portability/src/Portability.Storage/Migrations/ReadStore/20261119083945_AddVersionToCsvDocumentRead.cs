using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portability.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class AddVersionToCsvDocumentRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BE-140: таблица csv_document_read уже создана миграцией AddReadStores —
            // добавляем только Version (stale-skip QG-305), таблица не пересоздаётся.
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "csv_document_read",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "csv_document_read");
        }
    }
}
