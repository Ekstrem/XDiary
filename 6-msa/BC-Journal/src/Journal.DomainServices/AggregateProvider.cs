namespace Journal.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;

/// <summary>
/// QG-101/102/109: провайдер агрегата. Инжектит библиотечный IAnemicModelRepository (QG-109),
/// при промахе собирает агрегат из DefaultAnemicModel.Create (QG-102) и декорирует его Notifier'ом (QG-101).
/// </summary>
public sealed class AggregateProvider : IAggregateProvider<IJournal, IRecordAggregate>
{
    private readonly IAnemicModelRepository<IJournal, IJournalAnemicModel> _repository;

    public AggregateProvider(IAnemicModelRepository<IJournal, IJournalAnemicModel> repository) => _repository = repository;

    public async Task<IRecordAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return new Notifier(Aggregate.CreateInstance(model));
    }

    public async Task<IRecordAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return new Notifier(Aggregate.CreateInstance(model));
    }

    public IRecordAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IRecordAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}
