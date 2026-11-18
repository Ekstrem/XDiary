using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using Partners.Api.Events;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.InternalContracts;
using Xunit;

/// <summary>
/// BE-128: проверка, что доменное событие ЧЕРЕЗ ШИНУ доходит до консьюмера и
/// материализует read-модель (PublishAsync → OnNext → HandleAsync → UpsertAsync).
/// Юнит-тесты HandleAsync вызывают хендлер напрямую, а не через шину — этот тест закрывает
/// дыру «доходит ли вызов» (@TL 14:26 пункт (а)).
/// </summary>
public class PartnerEventBusProjectionTests
{
    [Fact]
    public void Bus_Publish_Projects_ReadModel()
    {
        var clientId = Guid.NewGuid();
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", clientId);

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        var readStore = new FakeReadStore();
        sc.AddSingleton(new PartnerEventHandler(new FakeStore(), readStore, new FakeRead(), new FakePublisher(), new FakeStore()));
        using var sp = sc.BuildServiceProvider();

        var consumer = sp.GetRequiredService<IEventBusConsumer>();
        var bus = sp.GetRequiredService<IEventBus>();
        consumer.Subscribe(sp.GetRequiredService<PartnerEventHandler>());

        bus.PublishAsync(reg.Event!);
        Thread.Sleep(200);

        Assert.NotNull(readStore.Upserted);
        Assert.Equal("Алиса", readStore.Upserted!.Name);
        Assert.Equal(clientId, readStore.Upserted.ClientId);
    }

    private sealed class FakeStore : IEventStoreRepository<IPartners, IPartnerAnemicModel>
    {
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(0);
        public Task<List<IPartnerAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IPartnerAnemicModel>());
        public Task<IPartnerAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IPartnerAnemicModel>(null!);
        public Task<IPartnerAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IPartnerAnemicModel>(null!);
    }

    private sealed class FakeReadStore : IReadModelStore<IPartners, PartnersReadModel>
    {
        public PartnersReadModel? Upserted;
        public Task UpsertAsync(PartnersReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeRead : IReadRepository<IPartners, PartnersReadModel>
    {
        public Task<PartnersReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<PartnersReadModel?>(null);
        public Task<IReadOnlyList<PartnersReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PartnersReadModel>>(new List<PartnersReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class FakePublisher : IPartnerEventPublisher
    {
        public Task PublishAsync(Guid partnerId, string name, Guid? clientId, DateTime? deletedAt, CancellationToken ct = default) => Task.CompletedTask;
    }
}
