namespace AppleHealthGateway.Api.Events;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.InternalContracts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// BE-140: консьюмер событий AppleHealthGateway. Без него `apple_health_read` остаётся пустой
/// (класс BE-134/BE-138: события пишутся в `Commands."DomainEvents"`, проекции нет).
/// </summary>
public sealed class AppleHealthGatewayEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public AppleHealthGatewayEventConsumer(IEventBusConsumer consumer, AppleHealthGatewayEventHandler handler)
    {
        _consumer = consumer;
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class AppleHealthGatewayEventHandler : IDomainEventHandler<IAppleHealthGateway>, IObserver<IDomainEvent<IAppleHealthGateway>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    // hosted service резолвится из root-scope, а IReadModelStore/IReadRepository — scoped (класс BE-134).
    public AppleHealthGatewayEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void OnNext(IDomainEvent<IAppleHealthGateway> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<IAppleHealthGateway> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var readStore = scope.ServiceProvider.GetRequiredService<IReadModelStore<IAppleHealthGateway, AppleImportReadModel>>();
        var readRepo = scope.ServiceProvider.GetRequiredService<IReadRepository<IAppleHealthGateway, AppleImportReadModel>>();
        var existing = await readRepo.GetByIdAsync(domainEvent.Id, default) ?? new AppleImportReadModel { Id = domainEvent.Id };
        // QG-305 + BE-137-класс: у legacy-строк Version — ms-таймстамп, у событий — 1,2,3.
        const long LegacyTimestampFloor = 1_000_000_000_000L;
        if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
        AppleHealthGatewayProjection.ApplyVos(existing, vos);
        existing.Version = domainEvent.Version;
        await readStore.UpsertAsync(existing);
    }
}

public static class AppleHealthGatewayProjection
{
    public static void ApplyVos(AppleImportReadModel m, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var r) && r is IAppleImportRoot root) { m.ClientId = root.ClientId; m.SourceId = root.SourceId; }
        if (vos.TryGetValue("ImportConfig", out var c) && c is IImportConfig cfg) m.Metrics = cfg.Metrics;
        if (vos.TryGetValue("LastImport", out var li) && li is StringValueObject liv) m.LastImport = liv.Value;
        if (vos.TryGetValue("ProcessedFiles", out var pf) && pf is StringListValueObject pfv) m.ProcessedFiles = pfv.Value;
    }
}
