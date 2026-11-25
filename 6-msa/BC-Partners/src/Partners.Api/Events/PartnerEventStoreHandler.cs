namespace Partners.Api.Events;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;

/// <summary>BE-148: durable-подписчик (IAsyncDomainEventHandler): шина await-ит HandleAsync до завершения PublishAsync, событие доступно replay сразу после команды. MV-проекции — отдельные асинхронные подписчики.</summary>
public sealed class PartnerEventStoreHandler : IAsyncDomainEventHandler<IPartners>
{
    private readonly IServiceScopeFactory _scopeFactory;
    public PartnerEventStoreHandler(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;


    public async Task HandleAsync(IDomainEvent<IPartners> domainEvent)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStoreRepository<IPartners, IPartnerAnemicModel>>();
        var serializer = ValueObjectSerializerFactory.CreateDefault();
        var entry = new DomainEventEntry
        {
            Id = domainEvent.Id,
            Version = domainEvent.Command.Version, // BE-150: Command.Version = новая версия агрегата
            CorrelationToken = domainEvent.CorrelationToken,
            BoundedContext = domainEvent.ContextName,
            CommandName = domainEvent.Command?.GetType().Name ?? string.Empty,
            SubjectName = domainEvent.ContextName,
            ChangedValueObjectsJson = serializer.Serialize(domainEvent.ChangedValueObjects as IReadOnlyDictionary<string, IValueObject>),
            Result = domainEvent.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        await store.SaveEventAsync(entry);
    }
}