namespace Catalog.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;

/// <summary>Провайдер агрегата (QG-101/102/109): IAnemicModelRepository + DefaultAnemicModel.Create + Notifier.</summary>
public sealed class AggregateProvider : IAggregateProvider<ICatalog, ICatalogAggregate>
{
    private readonly IAnemicModelRepository<ICatalog, ICatalogAnemicModel> _repository;
    public AggregateProvider(IAnemicModelRepository<ICatalog, ICatalogAnemicModel> repository) => _repository = repository;

    public async Task<ICatalogAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public async Task<ICatalogAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public ICatalogAggregate GetAggregate(Guid id, long version) => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();
    public ICatalogAggregate GetAggregate(Guid id) => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}
