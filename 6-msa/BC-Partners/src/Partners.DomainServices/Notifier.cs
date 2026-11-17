namespace Partners.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;

/// <summary>
/// QG-103: Notifier — декоратор бизнес-методов агрегата. Реализует IPartnerAggregate (т.е. IAggregate<IPartners>)
/// и пробрасывает результат каждого метода наблюдателям (IObservable) через реактивную обвязку.
/// </summary>
public sealed class Notifier : DomainEventNotifier<IPartners, IPartnerAggregate>, IPartnerAggregate,
    IObservable<AggregateResult<IPartners, IPartnerAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<IPartners, IPartnerAnemicModel>>> _observers = new();

    private Notifier(IPartnerAggregate aggregate) : base(aggregate) { }

    public static Notifier Create(IPartnerAggregate aggregate) => new(aggregate);

    // IAggregate<IPartners>:
    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    // IPartnerAnemicModel:
    public IPartnerRoot Root => Aggregate.Root;
    public string Photo => Aggregate.Photo;
    public string Gender => Aggregate.Gender;
    public string Notes => Aggregate.Notes;
    public Guid? ClientId => Aggregate.ClientId;
    public DateTime? DeletedAt => Aggregate.DeletedAt;

    // Бизнес-операции (6): каждая пробрасывает результат наблюдателям.
    public AggregateResult<IPartners, IPartnerAnemicModel> Register(string name, Guid clientId)
        => Observe(Aggregate.Register(name, clientId));
    public AggregateResult<IPartners, IPartnerAnemicModel> Rename(string name)
        => Observe(Aggregate.Rename(name));
    public AggregateResult<IPartners, IPartnerAnemicModel> UpdateProfile(string photo, string gender, string notes)
        => Observe(Aggregate.UpdateProfile(photo, gender, notes));
    public AggregateResult<IPartners, IPartnerAnemicModel> LinkClient(Guid clientId)
        => Observe(Aggregate.LinkClient(clientId));
    public AggregateResult<IPartners, IPartnerAnemicModel> UnlinkClient()
        => Observe(Aggregate.UnlinkClient());
    public AggregateResult<IPartners, IPartnerAnemicModel> Remove()
        => Observe(Aggregate.Remove());

    // Явная реализация IObservable (QG-007 не трогает контракт IObservable).
    IDisposable IObservable<AggregateResult<IPartners, IPartnerAnemicModel>>.Subscribe(IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<IPartners, IPartnerAnemicModel> Observe(AggregateResult<IPartners, IPartnerAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray())
            observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> _observer;

        public Subscription(Notifier notifier, IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> observer)
        {
            _notifier = notifier;
            _observer = observer;
        }

        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
