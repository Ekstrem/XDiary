namespace GarminGateway.Api.Events;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using GarminGateway.InternalContracts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// BE-140: консьюмер событий GarminGateway. Без него `garmin_sync_read` остаётся пустой
/// (класс BE-134/BE-138: события пишутся в `Commands."DomainEvents"`, проекции нет).
/// </summary>
public sealed class GarminGatewayEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public GarminGatewayEventConsumer(IEventBusConsumer consumer, GarminGatewayEventStoreHandler storeHandler, GarminGatewayEventHandler handler)
    {
        _consumer = consumer;
        _consumer.Subscribe(storeHandler);
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class GarminGatewayEventHandler : IDomainEventHandler<IGarminGateway>, IObserver<IDomainEvent<IGarminGateway>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    // hosted service резолвится из root-scope, а IReadModelStore/IReadRepository — scoped (класс BE-134).
    public GarminGatewayEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void OnNext(IDomainEvent<IGarminGateway> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<IGarminGateway> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var readStore = scope.ServiceProvider.GetRequiredService<IReadModelStore<IGarminGateway, GarminSyncReadModel>>();
        var readRepo = scope.ServiceProvider.GetRequiredService<IReadRepository<IGarminGateway, GarminSyncReadModel>>();
        var existing = await readRepo.GetByIdAsync(domainEvent.Id, default) ?? new GarminSyncReadModel { Id = domainEvent.Id };
        // QG-305 + BE-137-класс: у legacy-строк Version — ms-таймстамп, у событий — 1,2,3.
        const long LegacyTimestampFloor = 1_000_000_000_000L;
        if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
        GarminGatewayProjection.ApplyVos(existing, vos);
        existing.Version = domainEvent.Version;
        await readStore.UpsertAsync(existing);
    }
}

public static class GarminGatewayProjection
{
    public static void ApplyVos(GarminSyncReadModel m, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var r) && r is IGarminSyncRoot root) { m.ClientId = root.ClientId; m.SourceId = root.SourceId; }
        if (vos.TryGetValue("Credentials", out var cr) && cr is ICredentials creds)
        {
            m.ApiKey = creds.ApiKey;
            m.OauthToken = creds.OauthToken;
            m.TokenExpiry = creds.TokenExpiry;
        }
        if (vos.TryGetValue("SyncConfig", out var sc) && sc is ISyncConfig cfg) { m.Metrics = cfg.Metrics; m.WindowDays = cfg.WindowDays; }
        if (vos.TryGetValue("LastSync", out var ls) && ls is StringValueObject lsv) m.LastSync = lsv.Value;
    }
}
