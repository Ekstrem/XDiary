namespace Catalog.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;

/// <summary>BE-145: provider с сохранением старого контракта и completion-TCS для durable-publish.</summary>
public sealed class AggregateProvider : IAggregateProvider<ICatalog, ICatalogAggregate>, IAggregateProviderWithCompletion<ICatalog, ICatalogAggregate>
{
    private readonly IAnemicModelRepository<ICatalog, ICatalogAnemicModel> _repository;
    private readonly AggregateCompletion _completion;

    public AggregateProvider(IAnemicModelRepository<ICatalog, ICatalogAnemicModel> repository, AggregateCompletion completion)
    {
        _repository = repository;
        _completion = completion;
    }

    public async Task<(ICatalogAggregate Aggregate, Task Completion)> GetAggregateWithCompletionAsync(Guid id, CancellationToken cancellationToken)
        => (await GetAggregateAsync(id, cancellationToken), _completion.NewTask());

    public async Task<(ICatalogAggregate Aggregate, Task Completion)> GetAggregateWithCompletionAsync(Guid id, long version, CancellationToken cancellationToken)
        => (await GetAggregateAsync(id, version, cancellationToken), _completion.NewTask());

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

    public ICatalogAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public ICatalogAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}