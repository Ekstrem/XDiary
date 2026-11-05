using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partners.Storage.Migrations.EventStore
{
    /// <inheritdoc />
    public partial class AddEventStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Commands");

            migrationBuilder.CreateTable(
                name: "AggregateStates",
                schema: "Commands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    AggregateJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AggregateStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomainEvents",
                schema: "Commands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationToken = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundedContext = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CommandName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubjectName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ChangedValueObjectsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Result = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainEvents", x => new { x.Id, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                schema: "Commands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    AggregateJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => new { x.Id, x.Version });
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainEvents_CorrelationToken",
                schema: "Commands",
                table: "DomainEvents",
                column: "CorrelationToken");

            migrationBuilder.CreateIndex(
                name: "IX_DomainEvents_CreatedAt",
                schema: "Commands",
                table: "DomainEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_Id",
                schema: "Commands",
                table: "Snapshots",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AggregateStates",
                schema: "Commands");

            migrationBuilder.DropTable(
                name: "DomainEvents",
                schema: "Commands");

            migrationBuilder.DropTable(
                name: "Snapshots",
                schema: "Commands");
        }
    }
}
