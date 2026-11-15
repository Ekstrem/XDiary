using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Partners.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class AddReadStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BE-116: идемпотентность — CREATE TABLE IF NOT EXISTS вместо CreateTable,
            // чтобы повторный Migrate не падал 42P07 на существующей таблице.
            migrationBuilder.Sql(
@"CREATE TABLE IF NOT EXISTS partners_read (
    ""Id"" uuid NOT NULL,
    ""Name"" text NOT NULL,
    ""Photo"" text NOT NULL,
    ""Gender"" text NOT NULL,
    ""Notes"" text NOT NULL,
    ""ClientId"" uuid NULL,
    ""DeletedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_partners_read"" PRIMARY KEY (""Id"")
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partners_read");
        }
    }
}
