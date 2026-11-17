namespace Partners.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Microsoft.Extensions.Logging;
using Partners.Domain;
using Partners.Domain.Abstraction;

/// <summary>
/// Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301):
/// переопределяет GetById/GetByIdAndVersion/GetByCorrelationToken через PartnersEventReplay.
/// </summary>
public sealed class PartnersEventSourcingRepository : FullEventSourcingRepository<IPartners, IPartnerAnemicModel>
{
    private readonly PartnersEventReplay _replay;

    public PartnersEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<IPartners, IPartnerAnemicModel>> logger,
        PartnersEventReplay replay)
        : base(db, logger) => _replay = replay;

    public override async Task<List<IPartnerAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<IPartnerAnemicModel>() : new List<IPartnerAnemicModel> { model };
    }

    public override async Task<IPartnerAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"Partner {id} не найден на версии {version}.");
        return model;
    }

    public override Task<IPartnerAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается партнёрами.");
}
