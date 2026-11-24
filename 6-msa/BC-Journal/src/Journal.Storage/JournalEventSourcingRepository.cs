namespace Journal.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Microsoft.Extensions.Logging;

/// <summary>
/// Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301/override-план):
/// переопределяет GetById/GetByIdAndVersion/GetByCorrelationToken — вместо TODO-пустышки
/// восстанавливает модель из событий через RecordEventReplay (fold ChangedValueObjects).
/// </summary>
public sealed class JournalEventSourcingRepository : FullEventSourcingRepository<IJournal, IJournalAnemicModel>
{
    private readonly RecordEventReplay _replay;

    public JournalEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<IJournal, IJournalAnemicModel>> logger,
        RecordEventReplay replay,
        IEventStreamRehydrator<IJournal, IJournalAnemicModel> rehydrator)
        : base(db, logger, rehydrator) => _replay = replay;

    public override async Task<List<IJournalAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<IJournalAnemicModel>() : new List<IJournalAnemicModel> { model };
    }

    public override async Task<IJournalAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"Record {id} не найден на версии {version}.");
        return model;
    }

    public override Task<IJournalAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается журналом.");
}
