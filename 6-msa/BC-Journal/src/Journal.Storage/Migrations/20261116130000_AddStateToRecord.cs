using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Journal.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddStateToRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "records",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "State",
                table: "records");
        }
    }
}
