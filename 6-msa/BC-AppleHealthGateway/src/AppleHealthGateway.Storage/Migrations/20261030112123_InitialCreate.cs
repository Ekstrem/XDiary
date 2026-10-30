using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleHealthGateway.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "apple_import",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MetricsJson = table.Column<string>(type: "text", nullable: false),
                    LastImport = table.Column<string>(type: "text", nullable: false),
                    ProcessedFilesJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apple_import", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_apple_import_SourceId",
                table: "apple_import",
                column: "SourceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apple_import");
        }
    }
}
