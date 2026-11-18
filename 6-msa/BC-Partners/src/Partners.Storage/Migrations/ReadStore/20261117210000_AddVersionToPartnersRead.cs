using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partners.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    [DbContext(typeof(Partners.Storage.ReadDbContext))]
    [Migration("20261117210000_AddVersionToPartnersRead")]
    public partial class AddVersionToPartnersRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "partners_read",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "partners_read");
        }
    }
}
