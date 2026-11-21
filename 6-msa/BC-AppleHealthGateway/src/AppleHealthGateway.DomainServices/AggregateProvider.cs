namespace AppleHealthGateway.DomainServices;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;

/// <summary>BE-145: provider с сохранением старого контракта и completion-TCS для durable-publish.</summary>
public sealed class AggregateProvider : IAggregateProvider<IAppleHealthGateway, IAppleHealthGatewayAggregate>, IAggregateProviderWithCompletion<IAppleHealthGateway, IAppleHealthGatewayAggregate>
{
    private readonly IAnemicModelRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> _repository;
    private readonly AggregateCompletion _completion;

    public AggregateProvider(IAnemicModelRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> repository, AggregateCompletion completion)
    {
        _repository = repository;
        _completion = completion;
    }

    public async Task<(IAppleHealthGatewayAggregate Aggregate, Task Completion)> GetAggregateWithCompletionAsync(Guid id, CancellationToken cancellationToken)
        => (await GetAggregateAsync(id, cancellationToken), _completion.NewTask());

    public async Task<(IAppleHealthGatewayAggregate Aggregate, Task Completion)> GetAggregateWithCompletionAsync(Guid id, long version, CancellationToken cancellationToken)
        => (await GetAggregateAsync(id, version, cancellationToken), _completion.NewTask());

    public async Task<IAppleHealthGatewayAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var models = await _repository.GetById(id, cancellationToken);
        var model = models.FirstOrDefault() ?? DefaultAnemicModel.Create(id);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }

    public async Task<IAppleHealthGatewayAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken);
        return Notifier.Create(Aggregate.CreateInstance(model));
    }

    public IAppleHealthGatewayAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IAppleHealthGatewayAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).GetAwaiter().GetResult();
}