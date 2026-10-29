using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;

namespace DailyMetrics.DomainServices;

internal class AggregateProvider : IDailyMetricsAggregateProvider
{
    private readonly IRepository _repository;
    private readonly AbstractAggregateFactory<IDailyMetrics> _factory;
    private readonly IObserver<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>[] _observers;

    public AggregateProvider(
        IRepository repository,
        AbstractAggregateFactory<IDailyMetrics> factory,
        params IObserver<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>[] observers)
    {
        _repository = repository;
        _factory = factory;
        _observers = observers;
    }

    public async Task<IDailyMetricsAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = (await _repository.GetById(id, cancellationToken))?.FirstOrDefault()
            ?? DynamicAnemicModel<IDailyMetrics>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public async Task<IDailyMetricsAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken)
            ?? DynamicAnemicModel<IDailyMetrics>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public IDailyMetricsAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IDailyMetricsAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).Result;

    private async Task<IDailyMetricsAggregate> DecorateModel(IAnemicModel<IDailyMetrics> model, CancellationToken ct)
    {
        var metadata = await _factory.GetMetadataAsync("DailyMetrics", ct);
        var scope = _factory.BuildScope(metadata, model);
        var aggregate = Aggregate<IDailyMetrics>.CreateInstance(model, scope);
        var DailyMetricsAggregate = MetadataDailyMetricsAggregate.Create(aggregate, model, scope);
        return DailyMetricsAggregate.PipeNotifierToContract(_observers);
    }
}
