using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SomaticSeries.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    [DbContext(typeof(SomaticSeries.Storage.ReadDbContext))]
    [Migration("20261105170000_AddReadStores")]
    public partial class AddReadStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "somatic_read",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    T = table.Column<string>(type: "text", nullable: false),
                    Hr = table.Column<int>(type: "integer", nullable: false),
                    BodyBattery = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_somatic_read", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "somatic_read");
        }
    }
}
