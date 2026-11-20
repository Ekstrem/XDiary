namespace Portability.Api.Events;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;
using Portability.InternalContracts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// BE-140: консьюмер событий Portability. Без него `csv_document_read` остаётся пустой
/// (класс BE-134/BE-138: события пишутся в `Commands."DomainEvents"`, проекции нет).
/// </summary>
public sealed class PortabilityEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public PortabilityEventConsumer(IEventBusConsumer consumer, PortabilityEventStoreHandler storeHandler, PortabilityEventHandler handler)
    {
        _consumer = consumer;
        _consumer.Subscribe(storeHandler);
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class PortabilityEventHandler : IDomainEventHandler<IPortability>, IObserver<IDomainEvent<IPortability>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    // hosted service резолвится из root-scope, а IReadModelStore/IReadRepository — scoped (класс BE-134).
    public PortabilityEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void OnNext(IDomainEvent<IPortability> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<IPortability> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var readStore = scope.ServiceProvider.GetRequiredService<IReadModelStore<IPortability, CsvDocumentReadModel>>();
        var readRepo = scope.ServiceProvider.GetRequiredService<IReadRepository<IPortability, CsvDocumentReadModel>>();
        var existing = await readRepo.GetByIdAsync(domainEvent.Id, default) ?? new CsvDocumentReadModel { Id = domainEvent.Id };
        // QG-305 + BE-137-класс: у legacy-строк Version — ms-таймстамп, у событий — 1,2,3.
        const long LegacyTimestampFloor = 1_000_000_000_000L;
        if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
        PortabilityProjection.ApplyVos(existing, vos);
        existing.Version = domainEvent.Version;
        await readStore.UpsertAsync(existing);
    }
}

public static class PortabilityProjection
{
    public static void ApplyVos(CsvDocumentReadModel m, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var r) && r is ICsvDocumentRoot root) m.Headers = root.Headers;
        if (vos.TryGetValue("Rows", out var rw) && rw is StringListValueObject rv) m.Rows = rv.Value;
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m.ClientId = cv.Value;
    }
}
