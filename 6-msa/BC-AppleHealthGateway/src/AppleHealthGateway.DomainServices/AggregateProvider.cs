using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;

namespace AppleHealthGateway.DomainServices;

internal class AggregateProvider : IAppleImportAggregateProvider
{
    private readonly IRepository _repository;
    private readonly AbstractAggregateFactory<IAppleHealthGateway> _factory;
    private readonly IObserver<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>[] _observers;

    public AggregateProvider(
        IRepository repository,
        AbstractAggregateFactory<IAppleHealthGateway> factory,
        params IObserver<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>[] observers)
    {
        _repository = repository;
        _factory = factory;
        _observers = observers;
    }

    public async Task<IAppleImportAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = (await _repository.GetById(id, cancellationToken))?.FirstOrDefault()
            ?? DynamicAnemicModel<IAppleHealthGateway>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public async Task<IAppleImportAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken)
            ?? DynamicAnemicModel<IAppleHealthGateway>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public IAppleImportAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IAppleImportAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).Result;

    private async Task<IAppleImportAggregate> DecorateModel(IAnemicModel<IAppleHealthGateway> model, CancellationToken ct)
    {
        var metadata = await _factory.GetMetadataAsync("AppleImport", ct);
        var scope = _factory.BuildScope(metadata, model);
        var aggregate = Aggregate<IAppleHealthGateway>.CreateInstance(model, scope);
        var AppleHealthGatewayAggregate = MetadataAppleImportAggregate.Create(aggregate, model, scope);
        return AppleHealthGatewayAggregate.PipeNotifierToContract(_observers);
    }
}
