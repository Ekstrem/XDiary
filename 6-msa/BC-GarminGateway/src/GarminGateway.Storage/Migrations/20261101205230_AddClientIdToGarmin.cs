using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarminGateway.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIdToGarmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_garmin_sync_SourceId",
                table: "garmin_sync");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "garmin_sync",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_garmin_sync_ClientId_SourceId",
                table: "garmin_sync",
                columns: new[] { "ClientId", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_garmin_sync_ClientId_SourceId",
                table: "garmin_sync");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "garmin_sync");

            migrationBuilder.CreateIndex(
                name: "IX_garmin_sync_SourceId",
                table: "garmin_sync",
                column: "SourceId",
                unique: true);
        }
    }
}
