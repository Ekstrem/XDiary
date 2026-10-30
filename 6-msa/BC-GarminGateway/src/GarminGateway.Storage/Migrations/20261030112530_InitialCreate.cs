using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarminGateway.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "garmin_sync",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: false),
                    OauthToken = table.Column<string>(type: "text", nullable: false),
                    TokenExpiry = table.Column<string>(type: "text", nullable: false),
                    MetricsJson = table.Column<string>(type: "text", nullable: false),
                    WindowDays = table.Column<int>(type: "integer", nullable: false),
                    LastSync = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garmin_sync", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garmin_sync_SourceId",
                table: "garmin_sync",
                column: "SourceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "garmin_sync");
        }
    }
}
