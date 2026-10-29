using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;

namespace GarminGateway.DomainServices;

internal class AggregateProvider : IGarminSyncAggregateProvider
{
    private readonly IRepository _repository;
    private readonly AbstractAggregateFactory<IGarminGateway> _factory;
    private readonly IObserver<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>>[] _observers;

    public AggregateProvider(
        IRepository repository,
        AbstractAggregateFactory<IGarminGateway> factory,
        params IObserver<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>>[] observers)
    {
        _repository = repository;
        _factory = factory;
        _observers = observers;
    }

    public async Task<IGarminSyncAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = (await _repository.GetById(id, cancellationToken))?.FirstOrDefault()
            ?? DynamicAnemicModel<IGarminGateway>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public async Task<IGarminSyncAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken)
            ?? DynamicAnemicModel<IGarminGateway>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public IGarminSyncAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IGarminSyncAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).Result;

    private async Task<IGarminSyncAggregate> DecorateModel(IAnemicModel<IGarminGateway> model, CancellationToken ct)
    {
        var metadata = await _factory.GetMetadataAsync("GarminSync", ct);
        var scope = _factory.BuildScope(metadata, model);
        var aggregate = Aggregate<IGarminGateway>.CreateInstance(model, scope);
        var GarminGatewayAggregate = MetadataGarminSyncAggregate.Create(aggregate, model, scope);
        return GarminGatewayAggregate.PipeNotifierToContract(_observers);
    }
}
