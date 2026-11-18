namespace SomaticSeries.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;

/// <summary>Провайдер агрегата (QG-101/102/109): IAnemicModelRepository + DefaultAnemicModel.Create + Notifier.</summary>
public sealed class AggregateProvider : IAggregateProvider<ISomaticSeries, ISomaticSeriesAggregate>
{
    private readonly IAnemicModelRepository<ISomaticSeries, ISomaticSeriesAnemicModel> _repository;
    public AggregateProvider(IAnemicModelRepository<ISomaticSeries, ISomaticSeriesAnemicModel> repository) => _repository = repository;

    public async Task<ISomaticSeriesAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public async Task<ISomaticSeriesAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public ISomaticSeriesAggregate GetAggregate(Guid id, long version) => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();
    public ISomaticSeriesAggregate GetAggregate(Guid id) => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}
