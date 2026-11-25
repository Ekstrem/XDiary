namespace Journal.Api.Events;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;

/// <summary>BE-148: durable-подписчик (IAsyncDomainEventHandler): шина await-ит HandleAsync до завершения PublishAsync, событие доступно replay сразу после команды. MV-проекции — отдельные асинхронные подписчики.</summary>
public sealed class JournalEventStoreHandler : IAsyncDomainEventHandler<IJournal>
{
    private readonly IServiceScopeFactory _scopeFactory;
    public JournalEventStoreHandler(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;


    public async Task HandleAsync(IDomainEvent<IJournal> domainEvent)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStoreRepository<IJournal, IJournalAnemicModel>>();
        var serializer = new JsonEnvelopeValueObjectSerializer(new ValueObjectTypeRegistry(), new System.Text.Json.JsonSerializerOptions { Converters = { new Journal.Domain.Implementation.OrgasmsJsonConverter() } });
        var cmdName = domainEvent.ChangedValueObjects is not null
            && domainEvent.ChangedValueObjects.TryGetValue("CommandName", out var cn)
            && cn is Journal.Domain.Implementation.StringValueObject cns
            ? cns.Value
            : (domainEvent.Command?.GetType().Name ?? string.Empty);        var entry = new DomainEventEntry
        {
            Id = domainEvent.Id,
            Version = domainEvent.Command.Version, // BE-150: Command.Version = новая версия агрегата
            CorrelationToken = domainEvent.CorrelationToken,
            BoundedContext = domainEvent.ContextName,
            CommandName = cmdName,
            SubjectName = domainEvent.ContextName,
            ChangedValueObjectsJson = serializer.Serialize(domainEvent.ChangedValueObjects as IReadOnlyDictionary<string, IValueObject>),
            Result = domainEvent.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        await store.SaveEventAsync(entry);
    }
}