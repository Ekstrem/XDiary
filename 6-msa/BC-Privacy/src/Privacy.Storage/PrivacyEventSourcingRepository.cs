namespace Privacy.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Microsoft.Extensions.Logging;
using Privacy.Domain;
using Privacy.Domain.Abstraction;

/// <summary>Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301).</summary>
public sealed class PrivacyEventSourcingRepository : FullEventSourcingRepository<IPrivacy, IPinCredentialAnemicModel>
{
    private readonly PrivacyEventReplay _replay;
    public PrivacyEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<IPrivacy, IPinCredentialAnemicModel>> logger,
        PrivacyEventReplay replay)
        : base(db, logger) => _replay = replay;

    public override async Task<List<IPinCredentialAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<IPinCredentialAnemicModel>() : new List<IPinCredentialAnemicModel> { model };
    }
    public override async Task<IPinCredentialAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"PinCredential {id} не найден на версии {version}.");
        return model;
    }
    public override Task<IPinCredentialAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается.");
}
