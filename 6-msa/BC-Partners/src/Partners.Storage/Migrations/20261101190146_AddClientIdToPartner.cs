using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partners.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIdToPartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Self",
                table: "partners");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "partners",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "partners");

            migrationBuilder.AddColumn<bool>(
                name: "Self",
                table: "partners",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
