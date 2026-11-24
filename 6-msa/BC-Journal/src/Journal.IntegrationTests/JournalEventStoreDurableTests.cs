using Journal.Api.Events;
using Journal.DomainServices;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Journal.IntegrationTests;

/// <summary>BE-148: после await completion событие уже записано в event store — replay следующей команды его видит.</summary>
public class JournalEventStoreDurableTests
{
    [Fact]
    public async Task Completion_WhenDurableSavePending_ShouldWaitAndExposeEventToReplay()
    {
        var store = new DelayedStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IEventStoreRepository<IJournal, IJournalAnemicModel>>(store);
        using var sp = sc.BuildServiceProvider();
        var bus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
        bus.Subscribe<IJournal>(new JournalEventStoreHandler(sp.GetRequiredService<IServiceScopeFactory>()));

        var completion = new AggregateCompletion();
        var provider = new AggregateProvider(new EmptyRepository(), completion);
        var (aggregate, completionTask) = await provider.GetAggregateWithCompletionAsync(Guid.NewGuid(), default);
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(new BusAdapter(bus, completion));

        var result = aggregate.StartManualEntry(Guid.NewGuid());

        Assert.True(result.Result == DomainOperationResultEnum.Success);
        Assert.False(completionTask.IsCompleted);           // durable-запись ещё не завершена
        Assert.True(store.SaveStarted.Task.IsCompleted);   // хендлер вошёл в SaveEventAsync
        Assert.Equal(0, store.Saved.Count);                // событие ещё НЕ доступно replay

        store.Release();
        await completionTask;

        Assert.True(completionTask.IsCompletedSuccessfully);
        Assert.Single(store.Saved);                         // событие доступно replay сразу после команды
        Assert.Equal(1, store.Saved[0].Version);
    }

    [Fact]
    public async Task Completion_WhenDurableSaveFails_ShouldPropagateError()
    {
        var store = new FailingStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IEventStoreRepository<IJournal, IJournalAnemicModel>>(store);
        using var sp = sc.BuildServiceProvider();
        var bus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
        bus.Subscribe<IJournal>(new JournalEventStoreHandler(sp.GetRequiredService<IServiceScopeFactory>()));

        var completion = new AggregateCompletion();
        var provider = new AggregateProvider(new EmptyRepository(), completion);
        var (aggregate, completionTask) = await provider.GetAggregateWithCompletionAsync(Guid.NewGuid(), default);
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(new BusAdapter(bus, completion));

        var result = aggregate.StartManualEntry(Guid.NewGuid());

        Assert.True(result.Result == DomainOperationResultEnum.Success);
        await Assert.ThrowsAsync<InvalidOperationException>(() => completionTask); // ошибка durable пробрасывается
    }

    private sealed class EmptyRepository : IAnemicModelRepository<IJournal, IJournalAnemicModel>
    {
        public Task<List<IJournalAnemicModel>> GetById(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<IJournalAnemicModel>());
        public Task<IJournalAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IJournalAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class DelayedStore : BaseFakeStore
    {
        public readonly TaskCompletionSource SaveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release() => _release.TrySetResult();
        public override async Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default)
        {
            SaveStarted.TrySetResult();
            await _release.Task;
            Saved.Add(entry);
        }
    }

    private sealed class FailingStore : BaseFakeStore
    {
        public override Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default)
            => throw new InvalidOperationException("event store unavailable");
    }

    private abstract class BaseFakeStore : IEventStoreRepository<IJournal, IJournalAnemicModel>
    {
        public readonly List<DomainEventEntry> Saved = new();
        public abstract Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default);
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Saved.Count);
        public Task<List<IJournalAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IJournalAnemicModel>());
        public Task<IJournalAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IJournalAnemicModel>(null!);
        public Task<IJournalAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IJournalAnemicModel>(null!);
    }
}
