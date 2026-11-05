using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Journal.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class AddReadStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal_defaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastPartnerId = table.Column<string>(type: "text", nullable: false),
                    LastPlace = table.Column<string>(type: "text", nullable: false),
                    Places = table.Column<string>(type: "text", nullable: false),
                    Fantasies = table.Column<string>(type: "text", nullable: false),
                    Positions = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false),
                    Contraceptives = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_defaults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "journal_read",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Partners = table.Column<string>(type: "text", nullable: false),
                    PartnersNames = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<string>(type: "text", nullable: false),
                    DurationMin = table.Column<int>(type: "integer", nullable: false),
                    Place = table.Column<string>(type: "text", nullable: false),
                    Positions = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: false),
                    Costumes = table.Column<string>(type: "text", nullable: false),
                    Toys = table.Column<string>(type: "text", nullable: false),
                    Fantasies = table.Column<string>(type: "text", nullable: false),
                    Contraceptives = table.Column<string>(type: "text", nullable: false),
                    ContraceptiveCount = table.Column<int>(type: "integer", nullable: false),
                    Orgasms = table.Column<string>(type: "text", nullable: false),
                    Garmin = table.Column<bool>(type: "boolean", nullable: false),
                    GarminId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<string>(type: "text", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_read", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "partners_directory",
                columns: table => new
                {
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partners_directory", x => x.PartnerId);
                });

            migrationBuilder.CreateTable(
                name: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "journal_defaults");

            migrationBuilder.DropTable(
                name: "journal_read");

            migrationBuilder.DropTable(
                name: "partners_directory");

            migrationBuilder.DropTable(
                name: "records");
        }
    }
}
