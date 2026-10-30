using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Journal.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnersJson = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<string>(type: "text", nullable: false),
                    DurationMin = table.Column<int>(type: "integer", nullable: false),
                    Place = table.Column<string>(type: "text", nullable: false),
                    PositionsJson = table.Column<string>(type: "text", nullable: false),
                    TagsJson = table.Column<string>(type: "text", nullable: false),
                    CostumesJson = table.Column<string>(type: "text", nullable: false),
                    ToysJson = table.Column<string>(type: "text", nullable: false),
                    FantasiesJson = table.Column<string>(type: "text", nullable: false),
                    ContraceptivesJson = table.Column<string>(type: "text", nullable: false),
                    ContraceptiveCount = table.Column<int>(type: "integer", nullable: false),
                    OrgasmsJson = table.Column<string>(type: "text", nullable: false),
                    Garmin = table.Column<bool>(type: "boolean", nullable: false),
                    GarminId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_records_StartedAt",
                table: "records",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "records");
        }
    }
}
