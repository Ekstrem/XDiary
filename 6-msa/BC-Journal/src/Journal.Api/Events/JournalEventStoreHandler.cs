namespace Journal.Api.Events;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;

/// <summary>BE-143: подписчик шины, сохраняющий доменное событие в event store. Read-проекция — отдельный подписчик.</summary>
public sealed class JournalEventStoreHandler : IDomainEventHandler<IJournal>, IObserver<IDomainEvent<IJournal>>
{
    private readonly IServiceScopeFactory _scopeFactory;
    public JournalEventStoreHandler(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public void OnNext(IDomainEvent<IJournal> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event-store] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<IJournal> domainEvent)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStoreRepository<IJournal, IJournalAnemicModel>>();
        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new IValueObjectJsonConverter());
        var cmdName = domainEvent.ChangedValueObjects is not null
            && domainEvent.ChangedValueObjects.TryGetValue("CommandName", out var cn)
            && cn is Journal.Domain.Implementation.StringValueObject cns
            ? cns.Value
            : (domainEvent.Command?.GetType().Name ?? string.Empty);        var entry = new DomainEventEntry
        {
            Id = domainEvent.Id,
            Version = await store.GetEventCountAsync(domainEvent.Id) + 1,
            CorrelationToken = domainEvent.CorrelationToken,
            BoundedContext = domainEvent.ContextName,
            CommandName = cmdName,
            SubjectName = domainEvent.ContextName,
            ChangedValueObjectsJson = JsonSerializer.Serialize(domainEvent.ChangedValueObjects, jsonOptions),
            Result = domainEvent.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        await store.SaveEventAsync(entry);
    }
}