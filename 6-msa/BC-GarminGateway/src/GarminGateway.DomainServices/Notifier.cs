namespace GarminGateway.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;

/// <summary>Декоратор бизнес-методов агрегата (QG-103): пробрасывает результат наблюдателям.</summary>
public sealed class Notifier : DomainEventNotifier<IGarminGateway, IGarminGatewayAggregate>, IGarminGatewayAggregate,
    IObservable<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>> _observers = new();
    private Notifier(IGarminGatewayAggregate aggregate) : base(aggregate) { }
    public static Notifier Create(IGarminGatewayAggregate aggregate) => new(aggregate);

    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    public IGarminSyncRoot Root => Aggregate.Root;
    public ICredentials Credentials => Aggregate.Credentials;
    public ISyncConfig SyncConfig => Aggregate.SyncConfig;
    public string LastSync => Aggregate.LastSync;
    public AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Connect(Guid clientId, ICredentials credentials) => Observe(Aggregate.Connect(clientId, credentials));
    public AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Sync(ISyncConfig config) => Observe(Aggregate.Sync(config));
    public AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> FetchActivities() => Observe(Aggregate.FetchActivities());

    IDisposable IObservable<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>.Subscribe(IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Observe(AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray()) observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> _observer;
        public Subscription(Notifier notifier, IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> observer) { _notifier = notifier; _observer = observer; }
        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
