using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Reactive;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;

namespace Privacy.DomainServices;

/// <summary>
/// Декоратор агрегата PinCredential: проксирует бизнес-методы и рассылает результат подписчикам.
/// </summary>
internal class Notifier :
    DomainEventNotifier<IPrivacy, IPinCredentialAggregate>,
    IPinCredentialAggregate,
    IObservable<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>>
{
    private readonly List<IObserver<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>>> _observers;

    private Notifier(IPinCredentialAggregate aggregate, List<IObserver<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>>> observers)
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

    public AggregateResult<IPrivacy, IAnemicModel<IPrivacy>> SetPin(IAnemicModel<IPrivacy> model, ICommandToAggregate commandMetadata)
        => Aggregate.SetPin(model, commandMetadata).Do(MatchAndPushNotification);

    public AggregateResult<IPrivacy, IAnemicModel<IPrivacy>> RemovePin(IAnemicModel<IPrivacy> model, ICommandToAggregate commandMetadata)
        => Aggregate.RemovePin(model, commandMetadata).Do(MatchAndPushNotification);

    public IDisposable Subscribe(IObserver<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>> observer)
        => Unsubscriber<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>>.Subscribe(_observers, observer);

    private void MatchAndPushNotification(AggregateResult<IPrivacy, IAnemicModel<IPrivacy>> result)
    {
        foreach (var observer in _observers)
            observer.OnNext(result);
    }

    internal static IPinCredentialAggregate Create(
        IPinCredentialAggregate aggregate,
        List<IObserver<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>>> observers)
        => new Notifier(aggregate, observers);
}
