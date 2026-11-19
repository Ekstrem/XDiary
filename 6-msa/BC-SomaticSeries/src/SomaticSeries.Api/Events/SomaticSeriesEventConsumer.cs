namespace SomaticSeries.Api.Events;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;
using SomaticSeries.InternalContracts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// BE-140: консьюмер событий SomaticSeries. Без него `somatic_read` остаётся пустой:
/// события пишутся в `Commands."DomainEvents"`, но проекции нет (класс BE-134/BE-138).
/// </summary>
public sealed class SomaticSeriesEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public SomaticSeriesEventConsumer(IEventBusConsumer consumer, SomaticSeriesEventHandler handler)
    {
        _consumer = consumer;
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class SomaticSeriesEventHandler : IDomainEventHandler<ISomaticSeries>, IObserver<IDomainEvent<ISomaticSeries>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    // hosted service резолвится из root-scope, а IReadModelStore/IReadRepository — scoped (класс BE-134).
    public SomaticSeriesEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void OnNext(IDomainEvent<ISomaticSeries> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<ISomaticSeries> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var readStore = scope.ServiceProvider.GetRequiredService<IReadModelStore<ISomaticSeries, SomaticSeriesReadModel>>();
        var readRepo = scope.ServiceProvider.GetRequiredService<IReadRepository<ISomaticSeries, SomaticSeriesReadModel>>();
        var existing = await readRepo.GetByIdAsync(domainEvent.Id, default) ?? new SomaticSeriesReadModel { Id = domainEvent.Id };
        // QG-305 + BE-137-класс: у legacy-строк Version — ms-таймстамп, у событий — 1,2,3.
        const long LegacyTimestampFloor = 1_000_000_000_000L;
        if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
        SomaticSeriesProjection.ApplyVos(existing, vos);
        existing.Version = domainEvent.Version;
        await readStore.UpsertAsync(existing);
    }
}

public static class SomaticSeriesProjection
{
    public static void ApplyVos(SomaticSeriesReadModel m, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var r) && r is ISomaticPointRoot root) m.T = root.T;
        if (vos.TryGetValue("Hr", out var h) && h is IntValueObject hv) m.Hr = hv.Value;
        if (vos.TryGetValue("BodyBattery", out var b) && b is IntValueObject bv) m.BodyBattery = bv.Value;
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m.ClientId = cv.Value;
    }
}
