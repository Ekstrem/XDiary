using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Portability.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "csv_documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_csv_documents_Kind",
                table: "csv_documents",
                column: "Kind",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_csv_documents_Kind",
                table: "csv_documents");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "csv_documents");
        }
    }
}
