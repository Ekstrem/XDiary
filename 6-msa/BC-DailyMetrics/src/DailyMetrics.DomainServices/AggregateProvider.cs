namespace DailyMetrics.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;

/// <summary>BE-145: provider с сохранением старого контракта и completion-TCS для durable-publish.</summary>
public sealed class AggregateProvider : IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate>, IAggregateProviderWithCompletion<IDailyMetrics, IDailyMetricsAggregate>
{
    private readonly IAnemicModelRepository<IDailyMetrics, IDailyMetricsAnemicModel> _repository;
    private readonly AggregateCompletion _completion;

    public AggregateProvider(IAnemicModelRepository<IDailyMetrics, IDailyMetricsAnemicModel> repository, AggregateCompletion completion)
    {
        _repository = repository;
        _completion = completion;
    }

    public async Task<(IDailyMetricsAggregate Aggregate, Task Completion)> GetAggregateWithCompletionAsync(Guid id, CancellationToken cancellationToken)
        => (await GetAggregateAsync(id, cancellationToken), _completion.NewTask());

    public async Task<(IDailyMetricsAggregate Aggregate, Task Completion)> GetAggregateWithCompletionAsync(Guid id, long version, CancellationToken cancellationToken)
        => (await GetAggregateAsync(id, version, cancellationToken), _completion.NewTask());

    public async Task<IDailyMetricsAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }

    public async Task<IDailyMetricsAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }

    public IDailyMetricsAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IDailyMetricsAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}