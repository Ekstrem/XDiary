using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Journal.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class AddReadStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BE-116: идемпотентность. CreateTable генерирует CREATE TABLE без IF NOT EXISTS,
            // и при повторном Migrate (таблицы уже есть, а записи в __EFMigrationsHistory нет —
            // после EnsureCreated или предыдущего прогона) падает 42P07. Поэтому CREATE TABLE IF NOT EXISTS.
            migrationBuilder.Sql(
@"CREATE TABLE IF NOT EXISTS journal_defaults (
    ""Id"" uuid NOT NULL,
    ""ClientId"" uuid NOT NULL,
    ""LastPartnerId"" text NOT NULL,
    ""LastPlace"" text NOT NULL,
    ""Places"" text NOT NULL,
    ""Fantasies"" text NOT NULL,
    ""Positions"" text NOT NULL,
    ""Tags"" text NOT NULL,
    ""Contraceptives"" text NOT NULL,
    CONSTRAINT ""PK_journal_defaults"" PRIMARY KEY (""Id"")
);
CREATE TABLE IF NOT EXISTS journal_read (
    ""Id"" uuid NOT NULL,
    ""ClientId"" uuid NULL,
    ""Partners"" text NOT NULL,
    ""PartnersNames"" text NOT NULL,
    ""StartedAt"" text NOT NULL,
    ""DurationMin"" integer NOT NULL,
    ""Place"" text NOT NULL,
    ""Positions"" text NOT NULL,
    ""Tags"" text NOT NULL,
    ""Costumes"" text NOT NULL,
    ""Toys"" text NOT NULL,
    ""Fantasies"" text NOT NULL,
    ""Contraceptives"" text NOT NULL,
    ""ContraceptiveCount"" integer NOT NULL,
    ""Orgasms"" text NOT NULL,
    ""Garmin"" boolean NOT NULL,
    ""GarminId"" text NOT NULL,
    ""CreatedAt"" text NOT NULL,
    ""UpdatedAt"" text NOT NULL,
    ""Deleted"" boolean NOT NULL,
    CONSTRAINT ""PK_journal_read"" PRIMARY KEY (""Id"")
);
CREATE TABLE IF NOT EXISTS partners_directory (
    ""PartnerId"" uuid NOT NULL,
    ""Name"" text NOT NULL,
    ""ClientId"" uuid NULL,
    ""DeletedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_partners_directory"" PRIMARY KEY (""PartnerId"")
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journal_defaults");

            migrationBuilder.DropTable(
                name: "journal_read");

            migrationBuilder.DropTable(
                name: "partners_directory");
        }
    }
}
