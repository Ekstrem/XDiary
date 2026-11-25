namespace Journal.Api.Events;

using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Journal.InternalContracts;
using XDiary.Contracts;

/// <summary>BE-152: доменный IEventProjector для библиотечного EventStoreRebuildService.</summary>
public sealed class JournalEventProjector : IEventProjector
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IValueObjectSerializer _serializer;

    public JournalEventProjector(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _serializer = ValueObjectSerializerProvider.Create(options => options.Converters.Add(new OrgasmsJsonConverter()));
    }

    public async Task ProjectAsync(DomainEventEntry entry, CancellationToken ct = default)
    {
        var vos = ValueObjectSerializerProvider.DeserializeWithLegacy(_serializer, entry.ChangedValueObjectsJson);
        if (vos is null || vos.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var handler = new JournalEventHandler(
            sp.GetRequiredService<IReadModelStore<IJournal, JournalReadModel>>(),
            sp.GetRequiredService<IReadRepository<IJournal, JournalReadModel>>(),
            sp.GetRequiredService<IReadModelStore<IJournal, JournalDefaultsReadModel>>(),
            sp.GetRequiredService<IReadRepository<IJournal, JournalDefaultsReadModel>>());
        var command = CommandToAggregate.Commit(entry.CorrelationToken, entry.CommandName, entry.SubjectName, entry.Version);
        var domainEvent = new DomainEvent<IJournal>(
            entry.Id,
            entry.Version - 1,
            command,
            vos.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            BoundedContext<IJournal>.GetInfo(),
            DomainOperationResultEnum.Success,
            string.Empty);
        await handler.HandleAsync(domainEvent);
    }
}
