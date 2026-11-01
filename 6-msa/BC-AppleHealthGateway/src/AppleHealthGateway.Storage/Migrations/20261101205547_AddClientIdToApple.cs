using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleHealthGateway.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIdToApple : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_apple_import_SourceId",
                table: "apple_import");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "apple_import",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_apple_import_ClientId_SourceId",
                table: "apple_import",
                columns: new[] { "ClientId", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_apple_import_ClientId_SourceId",
                table: "apple_import");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "apple_import");

            migrationBuilder.CreateIndex(
                name: "IX_apple_import_SourceId",
                table: "apple_import",
                column: "SourceId",
                unique: true);
        }
    }
}
