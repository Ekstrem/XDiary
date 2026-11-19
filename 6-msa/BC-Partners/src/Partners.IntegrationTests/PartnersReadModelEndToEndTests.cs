using Partners.Api.Events;
using Partners.Application;
using Partners.Application.Commands;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.DomainServices;
using Partners.InternalContracts;
using Partners.Storage;
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

namespace Partners.IntegrationTests;

/// <summary>
/// BE-140 L4: сквозная проводка `command → event → bus → projection → read` для Partners на РЕАЛЬНОМ Postgres.
/// Главное — `ClientId` из актора доходит до read-модели.
/// </summary>
public class PartnersReadModelEndToEndTests
{
    private readonly ITestOutputHelper _out;
    public PartnersReadModelEndToEndTests(ITestOutputHelper output) => _out = output;

    [Trait("Requires", "Postgres")]
    [Fact]
    public async Task Register_Then_Read_FromDb_KeepsClientId()
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
            o => o.UseNpgsql(cs, x => x.MigrationsAssembly("Partners.Storage")));
        sc.AddEventStorePostgres<IPartners, IPartnerAnemicModel>(cs, null);
        sc.AddScoped<IEventStoreRepository<IPartners, IPartnerAnemicModel>, PartnersEventSourcingRepository>();
        sc.AddScoped<IAnemicModelRepository<IPartners, IPartnerAnemicModel>, PartnersEventSourcingRepository>();
        sc.AddScoped<PartnersEventReplay>();
        sc.AddReadStorePostgres<IPartners, PartnersReadModel, ReadDbContext>(cs, null);
        sc.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
        sc.AddScoped<IAggregateProvider<IPartners, IPartnerAggregate>, AggregateProvider>();
        sc.AddScoped<IObserver<AggregateResult<IPartners, IPartnerAnemicModel>>, BusAdapter>();

        var actor = new StubActor { ClientId = Guid.NewGuid() };
        sc.AddSingleton<IActorProvider>(actor);
        sc.AddSingleton<IPartnerEventPublisher, FakePublisher>();
        using var sp = sc.BuildServiceProvider();

        // ВНИМАНИЕ (BE-140, найдено тестом): PartnerEventHandler регистрируется AddSingleton,
        // а в ctor берёт scoped-зависимости — это класс BE-134 (долгоживущий DbContext).
        // Здесь собираем его с явными зависимостями, чтобы e2e не зависел от этого дефекта.
        var handler = new PartnerEventHandler(
            sp.GetRequiredService<IEventStoreRepository<IPartners, IPartnerAnemicModel>>(),
            sp.GetRequiredService<IReadModelStore<IPartners, PartnersReadModel>>(),
            sp.GetRequiredService<IReadRepository<IPartners, PartnersReadModel>>(),
            sp.GetRequiredService<IPartnerEventPublisher>(),
            sp.GetRequiredService<IAnemicModelRepository<IPartners, IPartnerAnemicModel>>());
        sp.GetRequiredService<IEventBusConsumer>().Subscribe(handler);

        var partnerId = Guid.NewGuid();
        var cmd = new RegisterPartnerHandler(
            sp.GetRequiredService<IAggregateProvider<IPartners, IPartnerAggregate>>(),
            sp.GetRequiredService<IObserver<AggregateResult<IPartners, IPartnerAnemicModel>>>(),
            actor);
        var res = await cmd.Handle(new RegisterPartnerCommand(partnerId, "Аня"), default);
        Assert.True(res.IsSuccess());

        await Task.Delay(700);

        var repo = sp.GetRequiredService<IReadRepository<IPartners, PartnersReadModel>>();
        var stored = await repo.GetByIdAsync(partnerId, default);
        Assert.NotNull(stored);
        Assert.Equal("Аня", stored!.Name);
        Assert.Equal(actor.ClientId, stored.ClientId);
    }

    private sealed class StubActor : IActorProvider
    {
        public Guid ClientId { get; set; }
    }

    private sealed class FakePublisher : IPartnerEventPublisher
    {
        public Task PublishAsync(Guid id, string name, Guid? clientId, DateTime? deletedAt, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
