namespace Journal.Api.Events;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Journal.InternalContracts;
using Microsoft.EntityFrameworkCore;
using XDiary.Contracts;

/// <summary>
/// BE-151: перестроение read-моделей Journal из Event Store (IRebuildService).
/// Идемпотентно: события применяются в порядке (Id, Version), stale-версии пропускает JournalEventHandler.
/// </summary>
public sealed class JournalRebuildService : IRebuildService<IJournal>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public JournalRebuildService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<EventStoreDbContext>();
        var handler = new JournalEventHandler(
            sp.GetRequiredService<IReadModelStore<IJournal, JournalReadModel>>(),
            sp.GetRequiredService<IReadRepository<IJournal, JournalReadModel>>(),
            sp.GetRequiredService<IReadModelStore<IJournal, JournalDefaultsReadModel>>(),
            sp.GetRequiredService<IReadRepository<IJournal, JournalDefaultsReadModel>>());
        var json = new JsonSerializerOptions();
        json.Converters.Add(new IValueObjectJsonConverter());
        json.Converters.Add(new OrgasmsJsonConverter());

        var entries = await db.Events.AsNoTracking()
            .OrderBy(e => e.Id).ThenBy(e => e.Version)
            .ToListAsync(ct);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var vos = JsonSerializer.Deserialize<IDictionary<string, IValueObject>>(entry.ChangedValueObjectsJson, json);
            if (vos is null || vos.Count == 0) continue;
            var command = CommandToAggregate.Commit(entry.CorrelationToken, entry.CommandName, entry.SubjectName, entry.Version);
            var domainEvent = new DomainEvent<IJournal>(
                entry.Id,
                entry.Version - 1, // acted-on версия при rebuild не используется для идемпотентности
                command,
                vos,
                BoundedContext<IJournal>.GetInfo(),
                DomainOperationResultEnum.Success,
                string.Empty);
            await handler.HandleAsync(domainEvent);
        }
    }
}
