namespace Client.Api.Events;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.InternalContracts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// E1: консьюмер событий Client. Подписывает хендлер на in-memory шину;
/// хендлер проектирует read-модель (client_read) из changed-VO (UpsertAsync).
/// Без этой проекции client_read остаётся пустой и GET /clients/me отдаёт 404 (BE-134).
/// </summary>
public sealed class ClientEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public ClientEventConsumer(IEventBusConsumer consumer, ClientEventHandler handler)
    {
        _consumer = consumer;
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class ClientEventHandler : IDomainEventHandler<IClients>, IObserver<IDomainEvent<IClients>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    // BE-134 fix: hosted service резолвится из root-scope, а IReadModelStore/IReadRepository —
    // scoped (root их резолвить не может, Autofac DependencyResolutionException).
    // Поэтому хендлер держит только IServiceScopeFactory (singleton) и резолвит scoped на каждое событие.
    public ClientEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void OnNext(IDomainEvent<IClients> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<IClients> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var readStore = scope.ServiceProvider.GetRequiredService<IReadModelStore<IClients, ClientReadModel>>();
        var readRepo = scope.ServiceProvider.GetRequiredService<IReadRepository<IClients, ClientReadModel>>();
        var existing = await readRepo.GetByIdAsync(domainEvent.Id, default) ?? new ClientReadModel { Id = domainEvent.Id };
        // BE-134 follow-up (@TL 04:55): register-событие несёт Version = ms-таймстамп (1795053…), а verify — маленькое число (2).
        // Без порога existing.Version (ms) >= domainEvent.Version (2) отбрасывает verify → verified остаётся false.
        const long LegacyTimestampFloor = 1_000_000_000_000L;
        if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
        ClientProjection.ApplyVos(existing, vos);
        existing.Version = domainEvent.Version;
        await readStore.UpsertAsync(existing);
    }
}

public static class ClientProjection
{
    // per-field: применяет только поля, которые реально есть в diff.
    public static void ApplyVos(ClientReadModel m, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var r) && r is IClientRoot root) m.Phone = root.Phone;
        if (vos.TryGetValue("DisplayName", out var d) && d is StringValueObject dv) m.DisplayName = dv.Value;
        if (vos.TryGetValue("Gender", out var g) && g is StringValueObject gv) m.Gender = gv.Value;
        if (vos.TryGetValue("PhotoUrl", out var p) && p is NullableStringValueObject pv) m.PhotoUrl = pv.Value;
        if (vos.TryGetValue("Verified", out var v) && v is BoolValueObject vv) m.Verified = vv.Value;
    }
}
