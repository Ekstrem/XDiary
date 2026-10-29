using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;

namespace Portability.DomainServices;

internal class AggregateProvider : ICsvDocumentAggregateProvider
{
    private readonly IRepository _repository;
    private readonly AbstractAggregateFactory<IPortability> _factory;
    private readonly IObserver<AggregateResult<IPortability, IAnemicModel<IPortability>>>[] _observers;

    public AggregateProvider(
        IRepository repository,
        AbstractAggregateFactory<IPortability> factory,
        params IObserver<AggregateResult<IPortability, IAnemicModel<IPortability>>>[] observers)
    {
        _repository = repository;
        _factory = factory;
        _observers = observers;
    }

    public async Task<ICsvDocumentAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = (await _repository.GetById(id, cancellationToken))?.FirstOrDefault()
            ?? DynamicAnemicModel<IPortability>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public async Task<ICsvDocumentAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken)
            ?? DynamicAnemicModel<IPortability>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public ICsvDocumentAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public ICsvDocumentAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).Result;

    private async Task<ICsvDocumentAggregate> DecorateModel(IAnemicModel<IPortability> model, CancellationToken ct)
    {
        var metadata = await _factory.GetMetadataAsync("CsvDocument", ct);
        var scope = _factory.BuildScope(metadata, model);
        var aggregate = Aggregate<IPortability>.CreateInstance(model, scope);
        var PortabilityAggregate = MetadataCsvDocumentAggregate.Create(aggregate, model, scope);
        return PortabilityAggregate.PipeNotifierToContract(_observers);
    }
}
