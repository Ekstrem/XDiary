using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GarminGateway.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class DropOauthTokenFromGarminSyncRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BE-142 (один токен, @AR 21:32): oauth_token больше не пользовательское поле —
            // колонка убирается; серверный маркер до реального Garmin API не хранится в read-модели.
            migrationBuilder.DropColumn(
                name: "OauthToken",
                table: "garmin_sync_read");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OauthToken",
                table: "garmin_sync_read",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
