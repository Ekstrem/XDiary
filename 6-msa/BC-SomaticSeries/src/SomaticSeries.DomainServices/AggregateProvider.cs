using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;

namespace SomaticSeries.DomainServices;

internal class AggregateProvider : ISomaticSeriesAggregateProvider
{
    private readonly IRepository _repository;
    private readonly AbstractAggregateFactory<ISomaticSeries> _factory;
    private readonly IObserver<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>>[] _observers;

    public AggregateProvider(
        IRepository repository,
        AbstractAggregateFactory<ISomaticSeries> factory,
        params IObserver<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>>[] observers)
    {
        _repository = repository;
        _factory = factory;
        _observers = observers;
    }

    public async Task<ISomaticPointAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = (await _repository.GetById(id, cancellationToken))?.FirstOrDefault()
            ?? DynamicAnemicModel<ISomaticSeries>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public async Task<ISomaticPointAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken)
            ?? DynamicAnemicModel<ISomaticSeries>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public ISomaticPointAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public ISomaticPointAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).Result;

    private async Task<ISomaticPointAggregate> DecorateModel(IAnemicModel<ISomaticSeries> model, CancellationToken ct)
    {
        var metadata = await _factory.GetMetadataAsync("SomaticPoint", ct);
        var scope = _factory.BuildScope(metadata, model);
        var aggregate = Aggregate<ISomaticSeries>.CreateInstance(model, scope);
        var SomaticPointAggregate = MetadataSomaticPointAggregate.Create(aggregate, model, scope);
        return SomaticPointAggregate.PipeNotifierToContract(_observers);
    }
}
