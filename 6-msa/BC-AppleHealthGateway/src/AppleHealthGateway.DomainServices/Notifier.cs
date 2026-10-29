using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Reactive;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;

namespace AppleHealthGateway.DomainServices;

/// <summary>
/// Декоратор агрегата AppleHealthGateway: проксирует бизнес-метод и рассылает результат подписчикам.
/// </summary>
internal class Notifier :
    DomainEventNotifier<IAppleHealthGateway, IAppleImportAggregate>,
    IAppleImportAggregate,
    IObservable<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>
{
    private readonly List<IObserver<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>> _observers;

    private Notifier(IAppleImportAggregate aggregate, List<IObserver<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>> observers)
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

    public AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>> UpsertImportState(IAnemicModel<IAppleHealthGateway> model, ICommandToAggregate commandMetadata)
        => Aggregate.UpsertImportState(model, commandMetadata).Do(MatchAndPushNotification);

    public IDisposable Subscribe(IObserver<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>> observer)
        => Unsubscriber<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>.Subscribe(_observers, observer);

    private void MatchAndPushNotification(AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>> result)
    {
        foreach (var observer in _observers)
            observer.OnNext(result);
    }

    internal static IAppleImportAggregate Create(
        IAppleImportAggregate aggregate,
        List<IObserver<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>> observers)
        => new Notifier(aggregate, observers);
}
