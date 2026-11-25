namespace SomaticSeries.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;

/// <summary>Декоратор бизнес-методов агрегата (QG-103): пробрасывает результат наблюдателям.</summary>
public sealed class Notifier : DomainEventNotifier<ISomaticSeries, ISomaticSeriesAggregate>, ISomaticSeriesAggregate,
    IObservable<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>>> _observers = new();
    private Notifier(ISomaticSeriesAggregate aggregate) : base(aggregate) { }
    public static Notifier Create(ISomaticSeriesAggregate aggregate) => new(aggregate);

    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    public ISomaticPointRoot Root => Aggregate.Root;
    public int Hr => Aggregate.Hr;
    public int BodyBattery => Aggregate.BodyBattery;
    public Guid? ClientId => Aggregate.ClientId;
    public AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> RecordPoint(string t, int hr, int bodyBattery, Guid clientId, CommandToAggregate? command = null) => Observe(Aggregate.RecordPoint(t, hr, bodyBattery, clientId, command));

    IDisposable IObservable<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>>.Subscribe(IObserver<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> Observe(AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray()) observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>> _observer;
        public Subscription(Notifier notifier, IObserver<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>> observer) { _notifier = notifier; _observer = observer; }
        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
