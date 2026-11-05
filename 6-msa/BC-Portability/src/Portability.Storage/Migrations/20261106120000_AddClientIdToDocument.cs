using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Portability.Storage;

#nullable disable

namespace Portability.Storage.Migrations
{
    /// <summary>
    /// BE-084(c): измерение владельца документа. Старые строки получают ClientId = NULL
    /// (не отдаются никому, пока не будет backfill). Уникальность — (ClientId, Kind), а не Kind.
    /// </summary>
    [DbContext(typeof(CommandDbContext))]
    [Migration("20261106120000_AddClientIdToDocument")]
    public partial class AddClientIdToDocument : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_csv_documents_Kind", table: "csv_documents");
            migrationBuilder.AddColumn<Guid>(name: "ClientId", table: "csv_documents", type: "uuid", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_csv_documents_ClientId_Kind", table: "csv_documents", columns: new[] { "ClientId", "Kind" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_csv_documents_ClientId_Kind", table: "csv_documents");
            migrationBuilder.DropColumn(name: "ClientId", table: "csv_documents");
            migrationBuilder.CreateIndex(name: "IX_csv_documents_Kind", table: "csv_documents", column: "Kind", unique: true);
        }
    }
}
