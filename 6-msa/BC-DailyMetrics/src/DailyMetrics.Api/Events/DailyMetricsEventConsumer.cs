namespace DailyMetrics.Api.Events;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;
using DailyMetrics.InternalContracts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// BE-140: консьюмер событий DailyMetrics. Без него `daily_metrics_read` остаётся пустой
/// (класс BE-134/BE-138: события пишутся в `Commands."DomainEvents"`, проекции нет).
/// </summary>
public sealed class DailyMetricsEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public DailyMetricsEventConsumer(IEventBusConsumer consumer, DailyMetricsEventStoreHandler storeHandler, DailyMetricsEventHandler handler)
    {
        _consumer = consumer;
        _consumer.Subscribe(storeHandler);
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class DailyMetricsEventHandler : IDomainEventHandler<IDailyMetrics>, IObserver<IDomainEvent<IDailyMetrics>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    // hosted service резолвится из root-scope, а IReadModelStore/IReadRepository — scoped (класс BE-134).
    public DailyMetricsEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void OnNext(IDomainEvent<IDailyMetrics> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<IDailyMetrics> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var readStore = scope.ServiceProvider.GetRequiredService<IReadModelStore<IDailyMetrics, DailyMetricsReadModel>>();
        var readRepo = scope.ServiceProvider.GetRequiredService<IReadRepository<IDailyMetrics, DailyMetricsReadModel>>();
        var existing = await readRepo.GetByIdAsync(domainEvent.Id, default) ?? new DailyMetricsReadModel { Id = domainEvent.Id };
        // QG-305 + BE-137-класс: у legacy-строк Version — ms-таймстамп, у событий — 1,2,3.
        const long LegacyTimestampFloor = 1_000_000_000_000L;
        if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
        DailyMetricsProjection.ApplyVos(existing, vos);
        existing.Version = domainEvent.Version;
        await readStore.UpsertAsync(existing);
    }
}

public static class DailyMetricsProjection
{
    public static void ApplyVos(DailyMetricsReadModel m, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var r) && r is IDailyMetricsRoot root) m.Date = root.Date;
        if (vos.TryGetValue("Stress", out var s) && s is IntValueObject sv) m.Stress = sv.Value;
        if (vos.TryGetValue("RestingHr", out var hr) && hr is IntValueObject hrv) m.RestingHr = hrv.Value;
        if (vos.TryGetValue("Hrv", out var hv) && hv is IntValueObject hvv) m.Hrv = hvv.Value;
        if (vos.TryGetValue("Sleep", out var sl) && sl is ISleep sleep)
        {
            m.SleepScore = sleep.Score;
            m.SleepDurationMin = sleep.DurationMin;
            m.SleepDeepMin = sleep.DeepMin;
            m.SleepLightMin = sleep.LightMin;
            m.SleepRemMin = sleep.RemMin;
            m.SleepAwakeMin = sleep.AwakeMin;
        }
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m.ClientId = cv.Value;
    }
}
