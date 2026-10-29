using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;

namespace Catalog.DomainServices;

internal class AggregateProvider : ICatalogAggregateProvider
{
    private readonly IRepository _repository;
    private readonly AbstractAggregateFactory<ICatalog> _factory;
    private readonly IObserver<AggregateResult<ICatalog, IAnemicModel<ICatalog>>>[] _observers;

    public AggregateProvider(
        IRepository repository,
        AbstractAggregateFactory<ICatalog> factory,
        params IObserver<AggregateResult<ICatalog, IAnemicModel<ICatalog>>>[] observers)
    {
        _repository = repository;
        _factory = factory;
        _observers = observers;
    }

    public async Task<ICatalogItemAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = (await _repository.GetById(id, cancellationToken))?.FirstOrDefault()
            ?? DynamicAnemicModel<ICatalog>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public async Task<ICatalogItemAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken)
            ?? DynamicAnemicModel<ICatalog>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public ICatalogItemAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public ICatalogItemAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).Result;

    private async Task<ICatalogItemAggregate> DecorateModel(IAnemicModel<ICatalog> model, CancellationToken ct)
    {
        var metadata = await _factory.GetMetadataAsync("CatalogItem", ct);
        var scope = _factory.BuildScope(metadata, model);
        var aggregate = Aggregate<ICatalog>.CreateInstance(model, scope);
        var CatalogItemAggregate = MetadataCatalogItemAggregate.Create(aggregate, model, scope);
        return CatalogItemAggregate.PipeNotifierToContract(_observers);
    }
}
