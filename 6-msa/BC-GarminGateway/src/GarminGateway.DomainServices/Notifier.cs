using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Reactive;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;

namespace GarminGateway.DomainServices;

/// <summary>
/// Декоратор агрегата GarminGateway: проксирует бизнес-метод и рассылает результат подписчикам.
/// </summary>
internal class Notifier :
    DomainEventNotifier<IGarminGateway, IGarminSyncAggregate>,
    IGarminSyncAggregate,
    IObservable<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>>
{
    private readonly List<IObserver<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>>> _observers;

    private Notifier(IGarminSyncAggregate aggregate, List<IObserver<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>>> observers)
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

    public AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>> UpsertSyncState(IAnemicModel<IGarminGateway> model, ICommandToAggregate commandMetadata)
        => Aggregate.UpsertSyncState(model, commandMetadata).Do(MatchAndPushNotification);

    public IDisposable Subscribe(IObserver<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>> observer)
        => Unsubscriber<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>>.Subscribe(_observers, observer);

    private void MatchAndPushNotification(AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>> result)
    {
        foreach (var observer in _observers)
            observer.OnNext(result);
    }

    internal static IGarminSyncAggregate Create(
        IGarminSyncAggregate aggregate,
        List<IObserver<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>>> observers)
        => new Notifier(aggregate, observers);
}
