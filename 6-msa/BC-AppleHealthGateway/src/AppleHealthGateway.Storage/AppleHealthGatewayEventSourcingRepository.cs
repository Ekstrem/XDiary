namespace AppleHealthGateway.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Microsoft.Extensions.Logging;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;

/// <summary>Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301).</summary>
public sealed class AppleHealthGatewayEventSourcingRepository : FullEventSourcingRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>
{
    private readonly AppleHealthGatewayEventReplay _replay;
    public AppleHealthGatewayEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> logger,
        AppleHealthGatewayEventReplay replay,
        IEventStreamRehydrator<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> rehydrator)
        : base(db, logger, rehydrator) => _replay = replay;

    public override async Task<List<IAppleHealthGatewayAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<IAppleHealthGatewayAnemicModel>() : new List<IAppleHealthGatewayAnemicModel> { model };
    }
    public override async Task<IAppleHealthGatewayAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"AppleHealth {id} не найден на версии {version}.");
        return model;
    }
    public override Task<IAppleHealthGatewayAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается.");
}
