namespace Portability.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Microsoft.Extensions.Logging;
using Portability.Domain;
using Portability.Domain.Abstraction;

/// <summary>Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301).</summary>
public sealed class PortabilityEventSourcingRepository : FullEventSourcingRepository<IPortability, ICsvDocumentAnemicModel>
{
    private readonly PortabilityEventReplay _replay;
    public PortabilityEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<IPortability, ICsvDocumentAnemicModel>> logger,
        PortabilityEventReplay replay,
        IEventStreamRehydrator<IPortability, ICsvDocumentAnemicModel> rehydrator)
        : base(db, logger, rehydrator) => _replay = replay;

    public override async Task<List<ICsvDocumentAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<ICsvDocumentAnemicModel>() : new List<ICsvDocumentAnemicModel> { model };
    }
    public override async Task<ICsvDocumentAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"CsvDocument {id} не найден на версии {version}.");
        return model;
    }
    public override Task<ICsvDocumentAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается.");
}
