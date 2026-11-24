namespace SomaticSeries.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Microsoft.Extensions.Logging;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;

/// <summary>Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301).</summary>
public sealed class SomaticSeriesEventSourcingRepository : FullEventSourcingRepository<ISomaticSeries, ISomaticSeriesAnemicModel>
{
    private readonly SomaticSeriesEventReplay _replay;
    public SomaticSeriesEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<ISomaticSeries, ISomaticSeriesAnemicModel>> logger,
        SomaticSeriesEventReplay replay,
        IEventStreamRehydrator<ISomaticSeries, ISomaticSeriesAnemicModel> rehydrator)
        : base(db, logger, rehydrator) => _replay = replay;

    public override async Task<List<ISomaticSeriesAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<ISomaticSeriesAnemicModel>() : new List<ISomaticSeriesAnemicModel> { model };
    }
    public override async Task<ISomaticSeriesAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"Somatic point {id} не найден на версии {version}.");
        return model;
    }
    public override Task<ISomaticSeriesAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается.");
}
