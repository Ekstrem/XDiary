using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Reactive;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;

namespace DailyMetrics.DomainServices;

/// <summary>
/// Декоратор агрегата DailyMetrics: проксирует бизнес-метод и рассылает результат подписчикам.
/// </summary>
internal class Notifier :
    DomainEventNotifier<IDailyMetrics, IDailyMetricsAggregate>,
    IDailyMetricsAggregate,
    IObservable<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>
{
    private readonly List<IObserver<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>> _observers;

    private Notifier(IDailyMetricsAggregate aggregate, List<IObserver<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>> observers)
        : base(aggregate) => _observers = observers;

    #region Aggregate Tech Props

    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    #endregion

    public AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>> ImportDaily(IAnemicModel<IDailyMetrics> model, ICommandToAggregate commandMetadata)
        => Aggregate.ImportDaily(model, commandMetadata).Do(MatchAndPushNotification);

    public IDisposable Subscribe(IObserver<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>> observer)
        => Unsubscriber<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>.Subscribe(_observers, observer);

    private void MatchAndPushNotification(AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>> result)
    {
        foreach (var observer in _observers)
            observer.OnNext(result);
    }

    internal static IDailyMetricsAggregate Create(
        IDailyMetricsAggregate aggregate,
        List<IObserver<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>> observers)
        => new Notifier(aggregate, observers);
}
