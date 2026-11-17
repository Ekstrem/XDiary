using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarminGateway.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class AddReadStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "garmin_sync_read",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: false),
                    OauthToken = table.Column<string>(type: "text", nullable: false),
                    TokenExpiry = table.Column<string>(type: "text", nullable: false),
                    Metrics = table.Column<string>(type: "text", nullable: false),
                    WindowDays = table.Column<int>(type: "integer", nullable: false),
                    LastSync = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garmin_sync_read", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "garmin_sync_read");
        }
    }
}
