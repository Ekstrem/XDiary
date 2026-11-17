namespace Privacy.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;

/// <summary>Провайдер агрегата (QG-101/102/109): IAnemicModelRepository + DefaultAnemicModel.Create + Notifier.</summary>
public sealed class AggregateProvider : IAggregateProvider<IPrivacy, IPinCredentialAggregate>
{
    private readonly IAnemicModelRepository<IPrivacy, IPinCredentialAnemicModel> _repository;
    public AggregateProvider(IAnemicModelRepository<IPrivacy, IPinCredentialAnemicModel> repository) => _repository = repository;

    public async Task<IPinCredentialAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public async Task<IPinCredentialAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public IPinCredentialAggregate GetAggregate(Guid id, long version) => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();
    public IPinCredentialAggregate GetAggregate(Guid id) => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}
