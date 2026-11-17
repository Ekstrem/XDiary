namespace GarminGateway.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Microsoft.Extensions.Logging;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;

/// <summary>Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301).</summary>
public sealed class GarminGatewayEventSourcingRepository : FullEventSourcingRepository<IGarminGateway, IGarminGatewayAnemicModel>
{
    private readonly GarminGatewayEventReplay _replay;
    public GarminGatewayEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<IGarminGateway, IGarminGatewayAnemicModel>> logger,
        GarminGatewayEventReplay replay)
        : base(db, logger) => _replay = replay;

    public override async Task<List<IGarminGatewayAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<IGarminGatewayAnemicModel>() : new List<IGarminGatewayAnemicModel> { model };
    }
    public override async Task<IGarminGatewayAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"Garmin {id} не найден на версии {version}.");
        return model;
    }
    public override Task<IGarminGatewayAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается.");
}
