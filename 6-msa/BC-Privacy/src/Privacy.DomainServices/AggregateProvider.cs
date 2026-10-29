using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;

namespace Privacy.DomainServices;

internal class AggregateProvider : IPrivacyAggregateProvider
{
    private readonly IRepository _repository;
    private readonly AbstractAggregateFactory<IPrivacy> _factory;
    private readonly IObserver<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>>[] _observers;

    public AggregateProvider(
        IRepository repository,
        AbstractAggregateFactory<IPrivacy> factory,
        params IObserver<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>>[] observers)
    {
        _repository = repository;
        _factory = factory;
        _observers = observers;
    }

    public async Task<IPinCredentialAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = (await _repository.GetById(id, cancellationToken))?.FirstOrDefault()
            ?? DynamicAnemicModel<IPrivacy>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public async Task<IPinCredentialAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken)
            ?? DynamicAnemicModel<IPrivacy>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public IPinCredentialAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IPinCredentialAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).Result;

    private async Task<IPinCredentialAggregate> DecorateModel(IAnemicModel<IPrivacy> model, CancellationToken ct)
    {
        var metadata = await _factory.GetMetadataAsync("PinCredential", ct);
        var scope = _factory.BuildScope(metadata, model);
        var aggregate = Aggregate<IPrivacy>.CreateInstance(model, scope);
        var PinCredentialAggregate = MetadataPinCredentialAggregate.Create(aggregate, model, scope);
        return PinCredentialAggregate.PipeNotifierToContract(_observers);
    }
}
