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

    public AggregateResult<IJournal, IAnemicModel<IJournal>> CreateRecord(IAnemicModel<IJournal> model)
        => Aggregate.CreateRecord(model).Do(MatchAndPushNotification);

    public AggregateResult<IJournal, IAnemicModel<IJournal>> UpdateRecord(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.UpdateRecord(model, commandMetadata).Do(MatchAndPushNotification);

    public AggregateResult<IJournal, IAnemicModel<IJournal>> DeleteRecord(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.DeleteRecord(model, commandMetadata).Do(MatchAndPushNotification);

    public AggregateResult<IJournal, IAnemicModel<IJournal>> ImportRecord(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Aggregate.ImportRecord(model, commandMetadata).Do(MatchAndPushNotification);

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
