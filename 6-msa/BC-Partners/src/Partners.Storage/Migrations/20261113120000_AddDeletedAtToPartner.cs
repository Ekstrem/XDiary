using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partners.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedAtToPartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "partners",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DeletedAt", table: "partners");
        }
    }
}