namespace GarminGateway.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;

/// <summary>Провайдер агрегата (QG-101/102/109): IAnemicModelRepository + DefaultAnemicModel.Create + Notifier.</summary>
public sealed class AggregateProvider : IAggregateProvider<IGarminGateway, IGarminGatewayAggregate>
{
    private readonly IAnemicModelRepository<IGarminGateway, IGarminGatewayAnemicModel> _repository;
    public AggregateProvider(IAnemicModelRepository<IGarminGateway, IGarminGatewayAnemicModel> repository) => _repository = repository;

    public async Task<IGarminGatewayAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public async Task<IGarminGatewayAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public IGarminGatewayAggregate GetAggregate(Guid id, long version) => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();
    public IGarminGatewayAggregate GetAggregate(Guid id) => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}
