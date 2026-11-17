namespace Journal.Storage;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;

/// <summary>
/// Адаптер библиотечного IAnemicModelRepository над событийным реплеем журнала.
/// QG-109: провайдер инжектит именно библиотечный IAnemicModelRepository, а не кастомный storage.
/// </summary>
public sealed class JournalAnemicModelRepository : IAnemicModelRepository<IJournal, IJournalAnemicModel>
{
    private readonly RecordEventReplay _replay;

    public JournalAnemicModelRepository(RecordEventReplay replay) => _replay = replay;

    public async Task<List<IJournalAnemicModel>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        return model is null ? new List<IJournalAnemicModel>() : new List<IJournalAnemicModel> { model };
    }

    public async Task<IJournalAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _replay.ReplayAsync(id, cancellationToken);
        if (model is null || model.Version != version)
            throw new InvalidOperationException($"Record {id} не найден на версии {version}.");
        return model;
    }

    public Task<IJournalAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetByCorrelationToken не поддерживается журналом.");
}
