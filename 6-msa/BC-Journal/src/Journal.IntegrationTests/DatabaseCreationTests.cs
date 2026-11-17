using System.Data;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.InternalContracts;
using Journal.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Journal.IntegrationTests;

/// <summary>
/// BE-118 (создание объектов БД): на свежем Postgres каждый контекст, который мигрируется
/// на старте (EventStore + Command + Read), должен пройти Migrate() без ошибки, заполнить
/// __EFMigrationsHistory и создать свои таблицы. Регистрация повторяет Program.cs (41-51).
/// Гоняется в CI, где @DO даёт Postgres (env XDIARY_TEST_POSTGRES); без БД — пропуск, не «зелёный по тишине».
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

        // Регистрация как в Program.cs: порядок важен (MigrationsAssembly до AddEventStorePostgres).
        var services = new ServiceCollection();
        services.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql(cs, x => x.MigrationsAssembly("Journal.Storage")));
        services.AddEventStorePostgres<IJournal, IJournalAnemicModel>(cs, null);
        services.AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>(cs, null);
        services.AddReadStorePostgres<IJournal, PartnersDirectoryReadModel, ReadDbContext>(cs, null);
        services.AddReadStorePostgres<IJournal, JournalDefaultsReadModel, ReadDbContext>(cs, null);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        // EventStore (DF).
        var es = sp.GetRequiredService<EventStoreDbContext>();
        es.Database.Migrate();
        Assert.Empty(es.Database.GetPendingMigrations());
        Assert.NotEmpty(es.Database.GetAppliedMigrations());

        // Read (AddReadStores — тот самый, что падал 42P07).
        var read = sp.GetRequiredService<ReadDbContext>();
        read.Database.Migrate();
        Assert.Empty(read.Database.GetPendingMigrations());
        Assert.Contains("20261105170525_AddReadStores", read.Database.GetAppliedMigrations());
        Assert.True(TableExists(read, "journal_read"));
        Assert.True(TableExists(read, "partners_directory"));
    }

    private static bool TableExists(DbContext ctx, string table)
    {
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = @t)";
        var p = cmd.CreateParameter();
        p.ParameterName = "t";
        p.Value = table;
        cmd.Parameters.Add(p);
        return (bool)cmd.ExecuteScalar()!;
    }
}
