using System.Data;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.InternalContracts;
using Partners.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Partners.IntegrationTests;

/// <summary>
/// BE-118 (создание объектов БД): EventStore + Command + Read мигрируются на свежем Postgres.
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

        var services = new ServiceCollection();
        services.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql(cs, x => x.MigrationsAssembly("Partners.Storage")));
        services.AddEventStorePostgres<IPartners, IPartnerAnemicModel>(cs, null);
        services.AddReadStorePostgres<IPartners, PartnersReadModel, ReadDbContext>(cs, null);
        services.AddDbContext<CommandDbContext>(o => o.UseNpgsql(cs));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var es = sp.GetRequiredService<EventStoreDbContext>();
        es.Database.Migrate();
        Assert.Empty(es.Database.GetPendingMigrations());
        Assert.NotEmpty(es.Database.GetAppliedMigrations());

        var cmd = sp.GetRequiredService<CommandDbContext>();
        cmd.Database.Migrate();
        Assert.Empty(cmd.Database.GetPendingMigrations());
        Assert.Contains("20261030112254_InitialCreate", cmd.Database.GetAppliedMigrations());
        Assert.True(TableExists(cmd, "partners"));

        var read = sp.GetRequiredService<ReadDbContext>();
        read.Database.Migrate();
        Assert.Empty(read.Database.GetPendingMigrations());
        Assert.Contains("20261105170447_AddReadStores", read.Database.GetAppliedMigrations());
        Assert.True(TableExists(read, "partners_read"));
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
