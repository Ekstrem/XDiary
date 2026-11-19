using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Client.Storage.Migrations.ReadStore
{
    /// <inheritdoc />
    public partial class NoOpSnapshotOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BE-135: no-op. Таблица client_read уже создана миграциями
            // AddReadStores (6 колонок) + AddVersionToClientRead (Version).
            // Миграция нужна только как носитель снапшота ReadDbContextModelSnapshot.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
