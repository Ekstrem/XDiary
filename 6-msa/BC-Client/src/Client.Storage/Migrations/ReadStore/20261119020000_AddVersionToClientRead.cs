using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Client.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    [DbContext(typeof(Client.Storage.ReadDbContext))]
    [Migration("20261119020000_AddVersionToClientRead")]
    public partial class AddVersionToClientRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "client_read",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Version", table: "client_read");
        }
    }
}
