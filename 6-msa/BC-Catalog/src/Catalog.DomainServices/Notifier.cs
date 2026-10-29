using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Reactive;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;

namespace Catalog.DomainServices;

/// <summary>
/// Р”РµРєРѕСЂР°С‚РѕСЂ Р°РіСЂРµРіР°С‚Р° CatalogItem: РїСЂРѕРєСЃРёСЂСѓРµС‚ Р±РёР·РЅРµСЃ-РјРµС‚РѕРґС‹ Рё СЂР°СЃСЃС‹Р»Р°РµС‚ СЂРµР·СѓР»СЊС‚Р°С‚ РїРѕРґРїРёСЃС‡РёРєР°Рј.
/// </summary>
internal class Notifier :
    DomainEventNotifier<ICatalog, ICatalogItemAggregate>,
    ICatalogItemAggregate,
    IObservable<AggregateResult<ICatalog, IAnemicModel<ICatalog>>>
{
    private readonly List<IObserver<AggregateResult<ICatalog, IAnemicModel<ICatalog>>>> _observers;

    private Notifier(ICatalogItemAggregate aggregate, List<IObserver<AggregateResult<ICatalog, IAnemicModel<ICatalog>>>> observers)
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

    public AggregateResult<ICatalog, IAnemicModel<ICatalog>> CreateItem(IAnemicModel<ICatalog> model)
        => Aggregate.CreateItem(model).Do(MatchAndPushNotification);

    public AggregateResult<ICatalog, IAnemicModel<ICatalog>> UpdateItem(IAnemicModel<ICatalog> model, ICommandToAggregate commandMetadata)
        => Aggregate.UpdateItem(model, commandMetadata).Do(MatchAndPushNotification);

    public AggregateResult<ICatalog, IAnemicModel<ICatalog>> DeleteItem(IAnemicModel<ICatalog> model, ICommandToAggregate commandMetadata)
        => Aggregate.DeleteItem(model, commandMetadata).Do(MatchAndPushNotification);

    public IDisposable Subscribe(IObserver<AggregateResult<ICatalog, IAnemicModel<ICatalog>>> observer)
        => Unsubscriber<AggregateResult<ICatalog, IAnemicModel<ICatalog>>>.Subscribe(_observers, observer);

    private void MatchAndPushNotification(AggregateResult<ICatalog, IAnemicModel<ICatalog>> result)
    {
        foreach (var observer in _observers)
            observer.OnNext(result);
    }

    internal static ICatalogItemAggregate Create(
        ICatalogItemAggregate aggregate,
        List<IObserver<AggregateResult<ICatalog, IAnemicModel<ICatalog>>>> observers)
        => new Notifier(aggregate, observers);
}
