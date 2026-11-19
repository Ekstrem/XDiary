namespace Privacy.Api.Events;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;
using Privacy.InternalContracts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// BE-140: консьюмер событий Privacy. Без него `pin_credential_read` остаётся пустой
/// (класс BE-134/BE-138: события пишутся в `Commands."DomainEvents"`, проекции нет).
/// </summary>
public sealed class PrivacyEventConsumer : BackgroundService
{
    private readonly IEventBusConsumer _consumer;

    public PrivacyEventConsumer(IEventBusConsumer consumer, PrivacyEventHandler handler)
    {
        _consumer = consumer;
        _consumer.Subscribe(handler);
        Console.WriteLine("[consumer] subscribed");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}

public sealed class PrivacyEventHandler : IDomainEventHandler<IPrivacy>, IObserver<IDomainEvent<IPrivacy>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    // hosted service резолвится из root-scope, а IReadModelStore/IReadRepository — scoped (класс BE-134).
    public PrivacyEventHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void OnNext(IDomainEvent<IPrivacy> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<IPrivacy> domainEvent)
    {
        var vos = domainEvent.ChangedValueObjects;
        if (vos is null || vos.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var readStore = scope.ServiceProvider.GetRequiredService<IReadModelStore<IPrivacy, PinCredentialReadModel>>();
        var readRepo = scope.ServiceProvider.GetRequiredService<IReadRepository<IPrivacy, PinCredentialReadModel>>();
        var existing = await readRepo.GetByIdAsync(domainEvent.Id, default) ?? new PinCredentialReadModel { Id = domainEvent.Id };
        // QG-305 + BE-137-класс: у legacy-строк Version — ms-таймстамп, у событий — 1,2,3.
        const long LegacyTimestampFloor = 1_000_000_000_000L;
        if (existing.Version >= domainEvent.Version && existing.Version < LegacyTimestampFloor) return;
        PrivacyProjection.ApplyVos(existing, vos);
        existing.Version = domainEvent.Version;
        await readStore.UpsertAsync(existing);
    }
}

public static class PrivacyProjection
{
    public static void ApplyVos(PinCredentialReadModel m, IDictionary<string, IValueObject> vos)
    {
        if (vos.TryGetValue("Root", out var r) && r is IPinCredentialRoot root)
        {
            m.ClientId = root.ClientId;
            m.Salt = root.Salt;
            m.Hash = root.Hash;
            m.Iterations = root.Iterations;
        }
        if (vos.TryGetValue("Locked", out var l) && l is BoolValueObject lv) m.Locked = lv.Value;
    }
}
