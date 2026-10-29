using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;

namespace Journal.DomainServices;

internal class AggregateProvider : IJournalAggregateProvider
{
    private readonly IRepository _repository;
    private readonly AbstractAggregateFactory<IJournal> _factory;
    private readonly IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>>[] _observers;

    public AggregateProvider(
        IRepository repository,
        AbstractAggregateFactory<IJournal> factory,
        params IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>>[] observers)
    {
        _repository = repository;
        _factory = factory;
        _observers = observers;
    }

    public async Task<IRecordAggregate> GetAggregateAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = (await _repository.GetById(id, cancellationToken))?.FirstOrDefault()
            ?? DynamicAnemicModel<IJournal>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public async Task<IRecordAggregate> GetAggregateAsync(Guid id, long version, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAndVersion(id, version, cancellationToken)
            ?? DynamicAnemicModel<IJournal>.CreateDefault(id);
        return await DecorateModel(model, cancellationToken);
    }

    public IRecordAggregate GetAggregate(Guid id, long version)
        => GetAggregateAsync(id, version, CancellationToken.None).GetAwaiter().GetResult();

    public IRecordAggregate GetAggregate(Guid id)
        => GetAggregateAsync(id, CancellationToken.None).Result;

    private async Task<IRecordAggregate> DecorateModel(IAnemicModel<IJournal> model, CancellationToken ct)
    {
        var metadata = await _factory.GetMetadataAsync("Record", ct);
        var scope = _factory.BuildScope(metadata, model);
        var aggregate = Aggregate<IJournal>.CreateInstance(model, scope);
        var recordAggregate = MetadataRecordAggregate.Create(aggregate, model, scope);
        return recordAggregate.PipeNotifierToContract(_observers);
    }
}
