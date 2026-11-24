namespace DailyMetrics.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;

/// <summary>Декоратор бизнес-методов агрегата (QG-103): пробрасывает результат наблюдателям.</summary>
public sealed class Notifier : DomainEventNotifier<IDailyMetrics, IDailyMetricsAggregate>, IDailyMetricsAggregate,
    IObservable<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>> _observers = new();
    private Notifier(IDailyMetricsAggregate aggregate) : base(aggregate) { }
    public static Notifier Create(IDailyMetricsAggregate aggregate) => new(aggregate);

    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    public IDailyMetricsRoot Root => Aggregate.Root;
    public int Stress => Aggregate.Stress;
    public int RestingHr => Aggregate.RestingHr;
    public int Hrv => Aggregate.Hrv;
    public ISleep Sleep => Aggregate.Sleep;
    public Guid? ClientId => Aggregate.ClientId;
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordStress(string date, int stress, Guid clientId, CommandToAggregate? command = null) => Observe(Aggregate.RecordStress(date, stress, clientId, command));
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordRestingHr(string date, int restingHr, Guid clientId, CommandToAggregate? command = null) => Observe(Aggregate.RecordRestingHr(date, restingHr, clientId, command));
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordHrv(string date, int hrv, Guid clientId, CommandToAggregate? command = null) => Observe(Aggregate.RecordHrv(date, hrv, clientId, command));
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordSleep(string date, ISleep sleep, Guid clientId, CommandToAggregate? command = null) => Observe(Aggregate.RecordSleep(date, sleep, clientId, command));

    IDisposable IObservable<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>.Subscribe(IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> Observe(AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray()) observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> _observer;
        public Subscription(Notifier notifier, IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> observer) { _notifier = notifier; _observer = observer; }
        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
