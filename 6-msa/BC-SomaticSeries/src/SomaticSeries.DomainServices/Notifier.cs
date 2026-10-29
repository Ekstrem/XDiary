using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Reactive;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;

namespace SomaticSeries.DomainServices;

/// <summary>
/// Декоратор агрегата SomaticPoint: проксирует бизнес-метод и рассылает результат подписчикам.
/// </summary>
internal class Notifier :
    DomainEventNotifier<ISomaticSeries, ISomaticPointAggregate>,
    ISomaticPointAggregate,
    IObservable<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>>
{
    private readonly List<IObserver<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>>> _observers;

    private Notifier(ISomaticPointAggregate aggregate, List<IObserver<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>>> observers)
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

    public AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>> ImportPoint(IAnemicModel<ISomaticSeries> model, ICommandToAggregate commandMetadata)
        => Aggregate.ImportPoint(model, commandMetadata).Do(MatchAndPushNotification);

    public IDisposable Subscribe(IObserver<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>> observer)
        => Unsubscriber<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>>.Subscribe(_observers, observer);

    private void MatchAndPushNotification(AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>> result)
    {
        foreach (var observer in _observers)
            observer.OnNext(result);
    }

    internal static ISomaticPointAggregate Create(
        ISomaticPointAggregate aggregate,
        List<IObserver<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>>> observers)
        => new Notifier(aggregate, observers);
}
