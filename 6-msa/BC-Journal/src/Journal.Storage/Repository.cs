using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.DomainServices;
using Journal.InternalContracts;
using Journal.Storage.Specifications;
using Microsoft.Extensions.Logging;

namespace Journal.Storage;

/// <summary>
/// Репозиторий агрегата Record: чтение/запись доменных событий (Event Sourcing).
/// </summary>
public class Repository : IRepository
{
    private readonly ILogger<Repository> _logger;
    private readonly IQueryRepository<DomainEventEventEntry> _eventRepository;
    private readonly CommandDbContext _dbContext;

    public Repository(
        ILogger<Repository> logger,
        IQueryRepository<DomainEventEventEntry> eventRepository,
        CommandDbContext dbContext)
    {
        _logger = logger;
        _eventRepository = eventRepository;
        _dbContext = dbContext;
    }

    public async Task<List<IAnemicModel<IJournal>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var entries = await _eventRepository.ListAsync(new GetByIdSpec(id), cancellationToken);
        if (entries == null || entries.Count == 0)
            return new List<IAnemicModel<IJournal>>();

        // Последнее состояние — запись с максимальной версией (сначала).
        return entries
            .OrderByDescending(e => e.Version)
            .Select(ToAnemicModel)
            .ToList();
    }

    public async Task<IAnemicModel<IJournal>?> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var entry = await _eventRepository.FindByAsync(new GetByIdAndVersionSpec(id, version), cancellationToken);
        return entry != null ? ToAnemicModel(entry) : DynamicAnemicModel<IJournal>.CreateDefault(id);
    }

    public async Task<IAnemicModel<IJournal>?> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
    {
        var entry = await _eventRepository.FindByAsync(new GetByCorrelationTokenSpec(correlationToken), cancellationToken);
        return entry != null ? ToAnemicModel(entry) : null;
    }

    /// <summary>
    /// Сохранить доменное событие в Command Store (event sourcing).
    /// Version хранится как новая версия (Command.Version — Unix ms).
    /// </summary>
    public async Task SaveEventAsync(IDomainEvent<IJournal> domainEvent, CancellationToken cancellationToken = default)
    {
        var entry = new DomainEventEventEntry
        {
            Id = domainEvent.Id,
            Version = domainEvent.Command.Version,
            CorrelationToken = domainEvent.Command.CorrelationToken,
            CommandName = domainEvent.Command.CommandName,
            SubjectName = domainEvent.Command.SubjectName,
            ChangedValueObjectsJson = DomainEventJson.Serialize(domainEvent.ChangedValueObjects),
            BoundedContext = domainEvent.ContextName,
            Result = (int)domainEvent.Result,
            Reason = domainEvent.Reason ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Events.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Событие сохранено: Id={Id}, Version={Version}, Command={Command}",
            entry.Id, entry.Version, entry.CommandName);
    }

    private static IAnemicModel<IJournal> ToAnemicModel(DomainEventEventEntry entry)
    {
        var valueObjects = DomainEventJson.Deserialize(entry.ChangedValueObjectsJson);
        return DynamicAnemicModel<IJournal>.Create(
            entry.Id,
            entry.Version,
            entry.CorrelationToken,
            entry.CommandName,
            entry.SubjectName,
            valueObjects);
    }
}
