using Catalog.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Catalog.IntegrationTests;

/// <summary>
/// BE-118 (создание объектов БД): ReadDbContext мигрируется на свежем Postgres (создаёт catalog_read).
/// Гоняется в CI, где @DO даёт Postgres (env XDIARY_TEST_POSTGRES); без БД — пропуск.
/// </summary>
public sealed class DatabaseCreationTests
{
    private readonly ITestOutputHelper _out;
    public DatabaseCreationTests(ITestOutputHelper output) => _out = output;

    [Trait("Requires", "Postgres")]
    [Fact]
    public void Migrate_CreatesSchema_OnFreshDatabase()
    {
        var cs = Environment.GetEnvironmentVariable("XDIARY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs))
        {
            _out.WriteLine("SKIPPED: XDIARY_TEST_POSTGRES не задан — тест создания БД работает только в CI (Postgres от @DO).");
            return;
        }
        using var read = new ReadDbContext(new DbContextOptionsBuilder<ReadDbContext>().UseNpgsql(cs).Options);
        read.Database.Migrate();
        Assert.Empty(read.Database.GetPendingMigrations());
    }
}
