using Journal.Api.Events;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using Microsoft.Extensions.DependencyInjection;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Xunit;

/// <summary>BE-143: event-store-подписчик присваивает событиям монотонные версии из хранилища.</summary>
public class JournalEventStoreHandlerTests
{
    private static CommandToAggregate Cmd(string name, long? version = null)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", version ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    [Fact]
    public async Task TwoCommands_WhenSavedTwice_ShouldProduceDistinctVersions()
    {
        var id = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var r1 = Aggregate.CreateInstance(new AnemicModel { Id = id }).StartManualEntry(clientId, Cmd("StartManualEntry", 1000));
        var r2 = Aggregate.CreateInstance(new AnemicModel { Id = id }).StartManualEntry(clientId, Cmd("StartManualEntry", 1001));

        var store = new CountingStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IEventStoreRepository<IJournal, IJournalAnemicModel>>(store);
        using var sp = sc.BuildServiceProvider();
        var handler = new JournalEventStoreHandler(sp.GetRequiredService<IServiceScopeFactory>());

        await handler.HandleAsync(r1.Event);
        await handler.HandleAsync(r2.Event);

        Assert.Equal(2, store.Saved.Count);
        Assert.Equal(r1.Event.Command.Version, store.Saved[0].Version);
        Assert.Equal(r2.Event.Command.Version, store.Saved[1].Version);
        Assert.True(store.Saved[1].Version > store.Saved[0].Version);
    }

    private sealed class CountingStore : IEventStoreRepository<IJournal, IJournalAnemicModel>
    {
        public List<DomainEventEntry> Saved = new();
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) { Saved.Add(entry); return Task.CompletedTask; }
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Saved.Count);
        public Task<List<IJournalAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IJournalAnemicModel>());
        public Task<IJournalAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IJournalAnemicModel>(null!);
        public Task<IJournalAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IJournalAnemicModel>(null!);
    }
}
