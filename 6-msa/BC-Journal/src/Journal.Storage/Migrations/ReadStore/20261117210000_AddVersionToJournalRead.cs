using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Journal.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute(typeof(Journal.Storage.ReadDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute("20261117210000_AddVersionToJournalRead")]
    public partial class AddVersionToJournalRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "journal_read",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "journal_read");
        }
    }
}
