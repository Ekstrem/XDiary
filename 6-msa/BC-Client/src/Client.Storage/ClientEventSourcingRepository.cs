namespace Client.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using Microsoft.Extensions.Logging;
using Client.Domain;
using Client.Domain.Abstraction;

/// <summary>Наследник библиотечного FullEventSourcingRepository с реальным реплеем (QG-301).</summary>
public sealed class ClientEventSourcingRepository : FullEventSourcingRepository<IClients, IClientAnemicModel>
{
    private readonly ClientEventReplay _replay;
    public ClientEventSourcingRepository(
        EventStoreDbContext db,
        ILogger<FullEventSourcingRepository<IClients, IClientAnemicModel>> logger,
        ClientEventReplay replay,
        IEventStreamRehydrator<IClients, IClientAnemicModel> rehydrator)
        : base(db, logger, rehydrator) => _replay = replay;

    public override async Task<List<IClientAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<IClientAnemicModel>() : new List<IClientAnemicModel> { model };
    }
    public override async Task<IClientAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"Client {id} не найден на версии {version}.");
        return model;
    }
    public override Task<IClientAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается клиентами.");
}
