using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Journal.DomainServices;
using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using XDiary.Contracts;
using Xunit;

namespace Journal.IntegrationTests;

/// <summary>BE-145: TCS-мост завершается на durable-publish, а не на проекции MV.</summary>
public class JournalAggregateCompletionTests
{
    private static CommandToAggregate Cmd(string name)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    [Fact]
    public async Task Completion_WhenPublishPending_ShouldWaitForDurablePublish()
    {
        var completion = new AggregateCompletion();
        var bus = new ControllableBus();
        var (aggregate, task) = await NewAggregate(completion);
        Subscribe(aggregate, new BusAdapter(bus, completion));

        var result = aggregate.StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));

        Assert.True(result.IsSuccess());
        Assert.False(task.IsCompleted);
        bus.PublishTcs.SetResult();
        await task;
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Completion_WhenPublishFails_ShouldPropagateError()
    {
        var completion = new AggregateCompletion();
        var bus = new ControllableBus();
        var (aggregate, task) = await NewAggregate(completion);
        Subscribe(aggregate, new BusAdapter(bus, completion));

        var result = aggregate.StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));

        Assert.True(result.IsSuccess());
        bus.PublishTcs.SetException(new InvalidOperationException("publish failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
    }

    [Fact]
    public void BusAdapter_OnFailedBusinessResult_ShouldCompleteWithoutPublish()
    {
        var completion = new AggregateCompletion();
        var bus = new ControllableBus();
        var adapter = new BusAdapter(bus, completion);
        var task = completion.NewTask();
        var created = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        var deleted = Aggregate.CreateInstance(created.Model()).Delete(Cmd("Delete"));
        var failed = Aggregate.CreateInstance(deleted.Model()).Delete(Cmd("Delete")); // повторное удаление — корректный failed-result

        adapter.OnNext(failed);

        Assert.False(failed.IsSuccess());
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(0, bus.PublishCalls);
    }

    private static async Task<(IRecordAggregate Aggregate, Task Completion)> NewAggregate(AggregateCompletion completion)
    {
        var provider = new AggregateProvider(new EmptyRepository(), completion);
        return await provider.GetAggregateWithCompletionAsync(Guid.NewGuid(), default);
    }

    private static void Subscribe(IRecordAggregate aggregate, BusAdapter adapter)
        => ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(adapter);

    private sealed class EmptyRepository : IAnemicModelRepository<IJournal, IJournalAnemicModel>
    {
        public Task<List<IJournalAnemicModel>> GetById(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<IJournalAnemicModel>());
        public Task<IJournalAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IJournalAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ControllableBus : IEventBus
    {
        public readonly TaskCompletionSource PublishTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int PublishCalls;
        public Task PublishAsync<TBoundedContext>(IDomainEvent<TBoundedContext> domainEvent) where TBoundedContext : IBoundedContext
        {
            PublishCalls++;
            return PublishTcs.Task;
        }
        public void Publish<TBoundedContext>(IDomainEvent<TBoundedContext> domainEvent) where TBoundedContext : IBoundedContext => PublishCalls++;
        public void Subscribe<TBoundedContext>(IDomainEventHandler<TBoundedContext> handler) where TBoundedContext : IBoundedContext { }
        public void Unsubscribe<TBoundedContext>(IDomainEventHandler<TBoundedContext> handler) where TBoundedContext : IBoundedContext { }
    }
}
