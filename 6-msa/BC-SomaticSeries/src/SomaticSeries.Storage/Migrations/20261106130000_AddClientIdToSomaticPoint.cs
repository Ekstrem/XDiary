using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SomaticSeries.Storage;

#nullable disable

namespace SomaticSeries.Storage.Migrations
{
    /// <summary>BE-084: измерение владельца точки. Старые строки — ClientId = NULL (не отдаются никому).</summary>
    [DbContext(typeof(CommandDbContext))]
    [Migration("20261106130000_AddClientIdToSomaticPoint")]
    public partial class AddClientIdToSomaticPoint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(name: "ClientId", table: "somatic_points", type: "uuid", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_somatic_points_ClientId", table: "somatic_points", column: "ClientId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_somatic_points_ClientId", table: "somatic_points");
            migrationBuilder.DropColumn(name: "ClientId", table: "somatic_points");
        }
    }
}
