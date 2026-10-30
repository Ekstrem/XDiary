using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SomaticSeries.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "somatic_points",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    T = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Hr = table.Column<int>(type: "integer", nullable: false),
                    BodyBattery = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_somatic_points", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_somatic_points_T",
                table: "somatic_points",
                column: "T");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "somatic_points");
        }
    }
}
