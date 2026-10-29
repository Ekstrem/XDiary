using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;

namespace Journal.DomainServices;

/// <summary>
/// Репозиторий анемичных моделей агрегата Record.
/// </summary>
public interface IRepository : IAnemicModelRepository<IJournal, IAnemicModel<IJournal>>
{
    /// <summary>
    /// Сохранить доменное событие в Command Store (event sourcing).
    /// </summary>
    Task SaveEventAsync(IDomainEvent<IJournal> domainEvent, CancellationToken cancellationToken = default);
}
