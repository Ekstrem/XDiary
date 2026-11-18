namespace Portability.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;

/// <summary>Провайдер агрегата (QG-101/102/109): IAnemicModelRepository + DefaultAnemicModel.Create + Notifier.</summary>
public sealed class AggregateProvider : IAggregateProvider<IPortability, ICsvDocumentAggregate>
{
    private readonly IAnemicModelRepository<IPortability, ICsvDocumentAnemicModel> _repository;
    public AggregateProvider(IAnemicModelRepository<IPortability, ICsvDocumentAnemicModel> repository) => _repository = repository;

    public async Task<ICsvDocumentAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public async Task<ICsvDocumentAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }
    public ICsvDocumentAggregate GetAggregate(Guid id, long version) => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();
    public ICsvDocumentAggregate GetAggregate(Guid id) => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}
