using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Client.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileToClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "clients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "clients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "clients",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DisplayName", table: "clients");
            migrationBuilder.DropColumn(name: "Gender", table: "clients");
            migrationBuilder.DropColumn(name: "PhotoUrl", table: "clients");
        }
    }
}