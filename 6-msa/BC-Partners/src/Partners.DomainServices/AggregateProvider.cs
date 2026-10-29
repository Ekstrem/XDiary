using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;

namespace Partners.DomainServices;

internal class AggregateProvider : IPartnersAggregateProvider
{
    private readonly IRepository _repository;
    private readonly AbstractAggregateFactory<IPartners> _factory;
    private readonly IObserver<AggregateResult<IPartners, IAnemicModel<IPartners>>>[] _observers;

    public AggregateProvider(
        IRepository repository,
        AbstractAggregateFactory<IPartners> factory,
        params IObserver<AggregateResult<IPartners, IAnemicModel<IPartners>>>[] observers)
    {
        _repository = repository;
        _factory = factory;
        _observers = observers;
    }

    public async Task<IPartnerAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = (await _repository.GetById(id, cancellationToken))?.FirstOrDefault()
            ?? DynamicAnemicModel<IPartners>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public async Task<IPartnerAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken)
            ?? DynamicAnemicModel<IPartners>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public IPartnerAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IPartnerAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).Result;

    private async Task<IPartnerAggregate> DecorateModel(IAnemicModel<IPartners> model, CancellationToken ct)
    {
        var metadata = await _factory.GetMetadataAsync("Partner", ct);
        var scope = _factory.BuildScope(metadata, model);
        var aggregate = Aggregate<IPartners>.CreateInstance(model, scope);
        var PartnerAggregate = MetadataPartnerAggregate.Create(aggregate, model, scope);
        return PartnerAggregate.PipeNotifierToContract(_observers);
    }
}
