using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Partners.Storage;

#nullable disable

namespace Partners.Storage.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CommandDbContext))]
    [Migration("20261113120000_AddDeletedAtToPartner")]
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