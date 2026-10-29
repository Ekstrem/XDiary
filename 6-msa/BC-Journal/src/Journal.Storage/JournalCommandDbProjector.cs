using DigiTFactory.Libraries.SeedWorks.Events;
using Journal.Domain;
using Journal.DomainServices;
using Microsoft.Extensions.Logging;

namespace Journal.Storage;

/// <summary>
/// Проектор в Command Store: подписывается на шину и сохраняет доменные события
/// (Event Sourcing). Работает параллельно с JournalProjectionHandler.
/// </summary>
public class JournalCommandDbProjector : IDomainEventHandler<IJournal>, IObserver<IDomainEvent<IJournal>>
{
    private readonly IRepository _repository;
    private readonly ILogger<JournalCommandDbProjector> _logger;

    public JournalCommandDbProjector(IRepository repository, ILogger<JournalCommandDbProjector> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
        => _logger.LogError(error, "Ошибка при сохранении доменного события в Command Store.");

    public void OnNext(IDomainEvent<IJournal> value)
        => _repository.SaveEventAsync(value).GetAwaiter().GetResult();
}
