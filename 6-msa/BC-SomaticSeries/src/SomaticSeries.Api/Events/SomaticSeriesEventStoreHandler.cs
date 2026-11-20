namespace SomaticSeries.Api.Events;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;

/// <summary>BE-143: подписчик шины, сохраняющий доменное событие в event store. Read-проекция — отдельный подписчик.</summary>
public sealed class SomaticSeriesEventStoreHandler : IDomainEventHandler<ISomaticSeries>, IObserver<IDomainEvent<ISomaticSeries>>
{
    private readonly IServiceScopeFactory _scopeFactory;
    public SomaticSeriesEventStoreHandler(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public void OnNext(IDomainEvent<ISomaticSeries> e) => _ = HandleAsync(e).ContinueWith(t =>
    {
        if (t.IsFaulted) Console.Error.WriteLine($"[event-store] {e.Id} failed: {t.Exception?.GetBaseException().Message}");
    }, TaskScheduler.Default);
    public void OnError(Exception error) { }
    public void OnCompleted() { }

    public async Task HandleAsync(IDomainEvent<ISomaticSeries> domainEvent)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStoreRepository<ISomaticSeries, ISomaticSeriesAnemicModel>>();
        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new IValueObjectJsonConverter());
        var entry = new DomainEventEntry
        {
            Id = domainEvent.Id,
            Version = await store.GetEventCountAsync(domainEvent.Id) + 1,
            CorrelationToken = domainEvent.CorrelationToken,
            BoundedContext = domainEvent.ContextName,
            CommandName = domainEvent.Command?.GetType().Name ?? string.Empty,
            SubjectName = domainEvent.ContextName,
            ChangedValueObjectsJson = JsonSerializer.Serialize(domainEvent.ChangedValueObjects, jsonOptions),
            Result = domainEvent.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        await store.SaveEventAsync(entry);
    }
}