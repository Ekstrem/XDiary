namespace Catalog.Api.Events;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;
using Catalog.InternalContracts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// BE-138: консьюмер событий Catalog. Подписывает хендлер на in-memory шину;
/// хендлер проектирует read-модель (catalog_read) из changed-VO (UpsertAsync).
/// Без этой проекции catalog_read остаётся пустой: POST /catalog → 200, а GET /catalog → [],
/// GET /catalog/{id} → 404 (та же ошибка, что BE-134 у Client — запись есть, чтения нет).
/// </summary>
public sealed class CatalogEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public CatalogEventConsumer(IEventBusConsumer consumer, CatalogEventStoreHandler storeHandler, CatalogEventHandler handler)
    {
        _consumer = consumer;
        _consumer.Subscribe(storeHandler);
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class CatalogEventHandler : IDomainEventHandler<ICatalog>, IObserver<IDomainEvent<ICatalog>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    // BE-134-класс: hosted service резолвится из root-scope, а IReadModelStore/IReadRepository — scoped
    // (Autofac DependencyResolutionException). Держим только IServiceScopeFactory и открываем scope на событие.
    public CatalogEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void OnNext(IDomainEvent<ICatalog> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<ICatalog> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var readStore = scope.ServiceProvider.GetRequiredService<IReadModelStore<ICatalog, CatalogReadModel>>();
        var readRepo = scope.ServiceProvider.GetRequiredService<IReadRepository<ICatalog, CatalogReadModel>>();
        var existing = await readRepo.GetByIdAsync(domainEvent.Id, default) ?? new CatalogReadModel { Id = domainEvent.Id };
        // QG-305 + BE-137-класс: у legacy-строк Version — ms-таймстамп, у событий — 1,2,3; без пола
        // existing.Version (ms) >= domainEvent.Version (малый) навсегда отбрасывает события.
        const long LegacyTimestampFloor = 1_000_000_000_000L;
        if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
        // BE-147: Remove несёт DeletedAt — удаляем строку из read-store, а не делаем Upsert.
        if (vos.TryGetValue("DeletedAt", out var d) && d is DeletedAtValueObject { Value: not null })
        {
            await readStore.DeleteAsync(domainEvent.Id, default);
            return;
        }
        CatalogProjection.ApplyVos(existing, vos);
        existing.Version = domainEvent.Version;
        await readStore.UpsertAsync(existing);
    }
}

public static class CatalogProjection
{
    // per-field: применяет только поля, которые реально есть в diff (отсутствие ключа ≠ пропуск).
    public static void ApplyVos(CatalogReadModel m, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var r) && r is ICatalogItemRoot root) { m.Kind = root.Kind; m.Name = root.Name; }
        if (vos.TryGetValue("ClientId", out var c) && c is ClientIdValueObject cv) m.ClientId = cv.Value;
        if (vos.TryGetValue("Photo", out var p) && p is StringValueObject pv) m.Photo = pv.Value;
        if (vos.TryGetValue("Notes", out var n) && n is StringValueObject nv) m.Notes = nv.Value;
        if (vos.TryGetValue("Brand", out var b) && b is StringValueObject bv) m.Brand = bv.Value;
        if (vos.TryGetValue("Archived", out var a) && a is BoolValueObject av) m.Archived = av.Value;
        if (vos.TryGetValue("FantasyPartner", out var f) && f is StringValueObject fv) m.FantasyPartner = fv.Value;
    }
}
