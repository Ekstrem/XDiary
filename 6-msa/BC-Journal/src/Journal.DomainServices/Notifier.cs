using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Reactive;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;

namespace Journal.DomainServices;

/// <summary>
/// Декоратор агрегата Record: проксирует бизнес-методы и рассылает результат подписчикам.
/// </summary>
internal class Notifier :
    DomainEventNotifier<IJournal, IRecordAggregate>,
    IRecordAggregate,
    IObservable<AggregateResult<IJournal, IAnemicModel<IJournal>>>
{
    private readonly List<IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>>> _observers;

    private Notifier(IRecordAggregate aggregate, List<IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>>> observers)
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

    public AggregateResult<IJournal, IAnemicModel<IJournal>> StartManualEntry(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.StartManualEntry(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> StartFromGarmin(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.StartFromGarmin(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> SpecifyWhen(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.SpecifyWhen(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> ChoosePartner(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.ChoosePartner(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> SpecifyPlace(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.SpecifyPlace(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> AddPositions(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.AddPositions(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> AddAccessories(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.AddAccessories(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> RecordContraception(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.RecordContraception(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> SpecifyDuration(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.SpecifyDuration(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> RecordOrgasms(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.RecordOrgasms(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> Complete(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.Complete(model, commandMetadata).Do(MatchAndPushNotification);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> Delete(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.Delete(model, commandMetadata).Do(MatchAndPushNotification);

    public IDisposable Subscribe(IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>> observer)
        => Unsubscriber<AggregateResult<IJournal, IAnemicModel<IJournal>>>.Subscribe(_observers, observer);

    private void MatchAndPushNotification(AggregateResult<IJournal, IAnemicModel<IJournal>> result)
    {
        foreach (var observer in _observers)
            observer.OnNext(result);
    }

    internal static IRecordAggregate Create(
        IRecordAggregate aggregate,
        List<IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>>> observers)
        => new Notifier(aggregate, observers);
}
