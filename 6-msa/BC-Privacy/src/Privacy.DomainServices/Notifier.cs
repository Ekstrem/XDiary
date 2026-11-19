namespace Privacy.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;

/// <summary>Декоратор бизнес-методов агрегата (QG-103): пробрасывает результат наблюдателям.</summary>
public sealed class Notifier : DomainEventNotifier<IPrivacy, IPinCredentialAggregate>, IPinCredentialAggregate,
    IObservable<AggregateResult<IPrivacy, IPinCredentialAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>>> _observers = new();
    private Notifier(IPinCredentialAggregate aggregate) : base(aggregate) { }
    public static Notifier Create(IPinCredentialAggregate aggregate) => new(aggregate);

    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    public IPinCredentialRoot Root => Aggregate.Root;
    public bool Locked => Aggregate.Locked;
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> SetPin(Guid clientId, string salt, string hash, int iterations) => Observe(Aggregate.SetPin(clientId, salt, hash, iterations));
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> ChangePin(string salt, string hash, int iterations) => Observe(Aggregate.ChangePin(salt, hash, iterations));
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> Remove() => Observe(Aggregate.Remove());
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> Lock() => Observe(Aggregate.Lock());
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> Unlock() => Observe(Aggregate.Unlock());

    IDisposable IObservable<AggregateResult<IPrivacy, IPinCredentialAnemicModel>>.Subscribe(IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<IPrivacy, IPinCredentialAnemicModel> Observe(AggregateResult<IPrivacy, IPinCredentialAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray()) observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> _observer;
        public Subscription(Notifier notifier, IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> observer) { _notifier = notifier; _observer = observer; }
        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
