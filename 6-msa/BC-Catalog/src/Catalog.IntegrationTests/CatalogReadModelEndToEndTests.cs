using Catalog.Api.Events;
using Catalog.Application;
using Catalog.Application.Commands;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.DomainServices;
using Catalog.InternalContracts;
using Catalog.Storage;
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

namespace Catalog.IntegrationTests;

/// <summary>
/// BE-140 L4: сквозная проводка `command → event → bus → projection → read` на РЕАЛЬНОМ Postgres.
/// Главное — `ClientId` из актора доходит до read-модели (то, что ошибочно искали в мёртвой `catalog_items`).
/// </summary>
public class CatalogReadModelEndToEndTests
{
    private readonly ITestOutputHelper _out;
    public CatalogReadModelEndToEndTests(ITestOutputHelper output) => _out = output;

    [Trait("Requires", "Postgres")]
    [Fact]
    public async Task Command_Then_Read_FromDb_KeepsClientId()
    {
        var cs = Environment.GetEnvironmentVariable("XDIARY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs))
        {
            _out.WriteLine("SKIPPED: XDIARY_TEST_POSTGRES не задан — e2e работает только в CI.");
            return;
        }

        var options = new DbContextOptionsBuilder<ReadDbContext>().UseNpgsql(cs, o => o.EnableRetryOnFailure()).Options;
        using (var migrate = new ReadDbContext(options)) migrate.Database.Migrate();

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        sc.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(
            o => o.UseNpgsql(cs, x => x.MigrationsAssembly("Catalog.Storage").EnableRetryOnFailure()));
        sc.AddEventStorePostgres<ICatalog, ICatalogAnemicModel>(cs, null);
        sc.AddScoped<IEventStoreRepository<ICatalog, ICatalogAnemicModel>, CatalogEventSourcingRepository>();
        sc.AddScoped<IAnemicModelRepository<ICatalog, ICatalogAnemicModel>, CatalogEventSourcingRepository>();
        sc.AddScoped<CatalogEventReplay>();
        sc.AddReadStorePostgres<ICatalog, CatalogReadModel, ReadDbContext>(cs, null);
        sc.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
        sc.AddScoped<IAggregateProvider<ICatalog, ICatalogAggregate>, AggregateProvider>();
        sc.AddScoped<IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>>, BusAdapter>();

        var actor = new StubActor { ClientId = Guid.NewGuid() };
        sc.AddSingleton<IActorProvider>(actor);
        using var sp = sc.BuildServiceProvider();

        // e2e: консьюмер подписан на шину
        var handler = new CatalogEventHandler(sp.GetRequiredService<IServiceScopeFactory>());
        sp.GetRequiredService<IEventBusConsumer>().Subscribe(handler);

        // команда (как из контроллера: ClientId берётся из актора)
        var cmd = new AddCatalogItemHandler(
            sp.GetRequiredService<IAggregateProvider<ICatalog, ICatalogAggregate>>(),
            sp.GetRequiredService<IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>>>(),
            actor);
        var res = await cmd.Handle(new AddCatalogItemCommand(null, "toy", "Ball"), default);
        Assert.True(res.IsSuccess());

        await Task.Delay(700);

        var repo = sp.GetRequiredService<IReadRepository<ICatalog, CatalogReadModel>>();
        var stored = await repo.GetByIdAsync(res.Model().Id, default);
        Assert.NotNull(stored);
        Assert.Equal("Ball", stored!.Name);
        Assert.Equal(actor.ClientId, stored.ClientId);
    }

    private sealed class StubActor : IActorProvider
    {
        public Guid ClientId { get; set; }
    }
}
