using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Privacy.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIdToPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pin_credential_SourceId",
                table: "pin_credential");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "pin_credential",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_pin_credential_ClientId_SourceId",
                table: "pin_credential",
                columns: new[] { "ClientId", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pin_credential_ClientId_SourceId",
                table: "pin_credential");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "pin_credential");

            migrationBuilder.CreateIndex(
                name: "IX_pin_credential_SourceId",
                table: "pin_credential",
                column: "SourceId",
                unique: true);
        }
    }
}
