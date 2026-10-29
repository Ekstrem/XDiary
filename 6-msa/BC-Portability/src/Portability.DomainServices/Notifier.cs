using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Reactive;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;

namespace Portability.DomainServices;

/// <summary>
/// Декоратор агрегата Portability: проксирует бизнес-метод и рассылает результат подписчикам.
/// </summary>
internal class Notifier :
    DomainEventNotifier<IPortability, ICsvDocumentAggregate>,
    ICsvDocumentAggregate,
    IObservable<AggregateResult<IPortability, IAnemicModel<IPortability>>>
{
    private readonly List<IObserver<AggregateResult<IPortability, IAnemicModel<IPortability>>>> _observers;

    private Notifier(ICsvDocumentAggregate aggregate, List<IObserver<AggregateResult<IPortability, IAnemicModel<IPortability>>>> observers)
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

    public AggregateResult<IPortability, IAnemicModel<IPortability>> UpsertDocument(IAnemicModel<IPortability> model, ICommandToAggregate commandMetadata)
        => Aggregate.UpsertDocument(model, commandMetadata).Do(MatchAndPushNotification);

    public IDisposable Subscribe(IObserver<AggregateResult<IPortability, IAnemicModel<IPortability>>> observer)
        => Unsubscriber<AggregateResult<IPortability, IAnemicModel<IPortability>>>.Subscribe(_observers, observer);

    private void MatchAndPushNotification(AggregateResult<IPortability, IAnemicModel<IPortability>> result)
    {
        foreach (var observer in _observers)
            observer.OnNext(result);
    }

    internal static ICsvDocumentAggregate Create(
        ICsvDocumentAggregate aggregate,
        List<IObserver<AggregateResult<IPortability, IAnemicModel<IPortability>>>> observers)
        => new Notifier(aggregate, observers);
}
