namespace DailyMetrics.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Microsoft.Extensions.Logging;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;

/// <summary>Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301).</summary>
public sealed class DailyMetricsEventSourcingRepository : FullEventSourcingRepository<IDailyMetrics, IDailyMetricsAnemicModel>
{
    private readonly DailyMetricsEventReplay _replay;
    public DailyMetricsEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<IDailyMetrics, IDailyMetricsAnemicModel>> logger,
        DailyMetricsEventReplay replay)
        : base(db, logger) => _replay = replay;

    public override async Task<List<IDailyMetricsAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<IDailyMetricsAnemicModel>() : new List<IDailyMetricsAnemicModel> { model };
    }
    public override async Task<IDailyMetricsAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"Daily metrics {id} не найдены на версии {version}.");
        return model;
    }
    public override Task<IDailyMetricsAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается.");
}
