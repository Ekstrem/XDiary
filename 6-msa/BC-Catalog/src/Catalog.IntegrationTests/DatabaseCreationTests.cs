using Catalog.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Catalog.IntegrationTests;

/// <summary>
/// BE-118 (создание объектов БД): CommandDbContext мигрируется на свежем Postgres.
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
        using var cmd = new CommandDbContext(new DbContextOptionsBuilder<CommandDbContext>().UseNpgsql(cs).Options);
        cmd.Database.Migrate();
        Assert.Empty(cmd.Database.GetPendingMigrations());
        Assert.NotEmpty(cmd.Database.GetAppliedMigrations());
    }
}