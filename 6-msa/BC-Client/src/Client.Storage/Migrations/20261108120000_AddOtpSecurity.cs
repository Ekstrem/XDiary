using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Client.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OtpExpiresAt",
                table: "clients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OtpAttempts",
                table: "clients",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OtpExpiresAt", table: "clients");
            migrationBuilder.DropColumn(name: "OtpAttempts", table: "clients");
        }
    }
}