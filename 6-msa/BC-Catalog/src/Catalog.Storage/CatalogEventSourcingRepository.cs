namespace Catalog.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Microsoft.Extensions.Logging;
using Catalog.Domain;
using Catalog.Domain.Abstraction;

/// <summary>Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301).</summary>
public sealed class CatalogEventSourcingRepository : FullEventSourcingRepository<ICatalog, ICatalogAnemicModel>
{
    private readonly CatalogEventReplay _replay;
    public CatalogEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<ICatalog, ICatalogAnemicModel>> logger,
        CatalogEventReplay replay,
        IEventStreamRehydrator<ICatalog, ICatalogAnemicModel> rehydrator)
        : base(db, logger, rehydrator) => _replay = replay;

    public override async Task<List<ICatalogAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<ICatalogAnemicModel>() : new List<ICatalogAnemicModel> { model };
    }
    public override async Task<ICatalogAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"Catalog item {id} не найден на версии {version}.");
        return model;
    }
    public override Task<ICatalogAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается каталогом.");
}
