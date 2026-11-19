using Journal.Api.Events;
using Journal.Application;
using Journal.Application.Commands;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using Journal.InternalContracts;
using Journal.Storage;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace Journal.IntegrationTests;

/// <summary>
/// BE-140 L4: сквозная проводка `command → event → bus → projection → read` для Journal на РЕАЛЬНОМ Postgres.
/// Главное — `ClientId` из актора доходит до `journal_read`.
/// </summary>
public class JournalReadModelEndToEndTests
{
    private readonly ITestOutputHelper _out;
    public JournalReadModelEndToEndTests(ITestOutputHelper output) => _out = output;

    [Trait("Requires", "Postgres")]
    [Fact]
    public async Task StartManualEntry_Then_Read_FromDb_KeepsClientId()
    {
        var cs = Environment.GetEnvironmentVariable("XDIARY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs))
        {
            _out.WriteLine("SKIPPED: XDIARY_TEST_POSTGRES не задан — e2e работает только в CI.");
            return;
        }

        var options = new DbContextOptionsBuilder<ReadDbContext>().UseNpgsql(cs).Options;
        using (var migrate = new ReadDbContext(options)) migrate.Database.Migrate();

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        sc.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(
            o => o.UseNpgsql(cs, x => x.MigrationsAssembly("Journal.Storage")));
        sc.AddEventStorePostgres<IJournal, IJournalAnemicModel>(cs, null);
        sc.AddScoped<IEventStoreRepository<IJournal, IJournalAnemicModel>, JournalEventSourcingRepository>();
        sc.AddScoped<IAnemicModelRepository<IJournal, IJournalAnemicModel>, JournalEventSourcingRepository>();
        sc.AddScoped<RecordEventReplay>();
        sc.AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>(cs, null);
        sc.AddReadStorePostgres<IJournal, JournalDefaultsReadModel, ReadDbContext>(cs, null);
        sc.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
        sc.AddScoped<IAggregateProvider<IJournal, IRecordAggregate>, AggregateProvider>();
        sc.AddScoped<IObserver<AggregateResult<IJournal, IJournalAnemicModel>>, BusAdapter>();

        var actor = new StubActor { ClientId = Guid.NewGuid() };
        sc.AddSingleton<IActorProvider>(actor);
        using var sp = sc.BuildServiceProvider();

        // ВНИМАНИЕ (BE-140): JournalEventHandler регистрируется AddSingleton, а в ctor берёт
        // scoped-зависимости — класс BE-134 (то же, что у Partners). Собираем явно.
        var handler = new JournalEventHandler(
            sp.GetRequiredService<IReadModelStore<IJournal, JournalReadModel>>(),
            sp.GetRequiredService<IReadRepository<IJournal, JournalReadModel>>(),
            sp.GetRequiredService<IReadModelStore<IJournal, JournalDefaultsReadModel>>(),
            sp.GetRequiredService<IReadRepository<IJournal, JournalDefaultsReadModel>>());
        sp.GetRequiredService<IEventBusConsumer>().Subscribe(handler);

        var entryId = Guid.NewGuid();
        var cmd = new StartManualEntryHandler(
            sp.GetRequiredService<IAggregateProvider<IJournal, IRecordAggregate>>(),
            sp.GetRequiredService<IObserver<AggregateResult<IJournal, IJournalAnemicModel>>>(),
            actor);
        var res = await cmd.Handle(new StartManualEntryCommand(entryId), default);
        Assert.True(res.IsSuccess());

        await Task.Delay(700);

        var repo = sp.GetRequiredService<IReadRepository<IJournal, JournalReadModel>>();
        var stored = await repo.GetByIdAsync(entryId, default);
        Assert.NotNull(stored);
        Assert.Equal(actor.ClientId, stored!.ClientId);
    }

    private sealed class StubActor : IActorProvider
    {
        public Guid ClientId { get; set; }
    }
}
