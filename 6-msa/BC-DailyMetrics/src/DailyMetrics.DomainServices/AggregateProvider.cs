namespace DailyMetrics.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;

/// <summary>Провайдер агрегата (QG-101/102/109): IAnemicModelRepository + DefaultAnemicModel.Create + Notifier.</summary>
public sealed class AggregateProvider : IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate>
{
    private readonly IAnemicModelRepository<IDailyMetrics, IDailyMetricsAnemicModel> _repository;
    public AggregateProvider(IAnemicModelRepository<IDailyMetrics, IDailyMetricsAnemicModel> repository) => _repository = repository;

    public async Task<IDailyMetricsAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public async Task<IDailyMetricsAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public IDailyMetricsAggregate GetAggregate(Guid id, long version) => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();
    public IDailyMetricsAggregate GetAggregate(Guid id) => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}
