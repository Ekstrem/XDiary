using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Monads;
using DigiTFactory.Libraries.SeedWorks.Reactive;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;

namespace Partners.DomainServices;

/// <summary>
/// Р”РµРєРѕСЂР°С‚РѕСЂ Р°РіСЂРµРіР°С‚Р° Partner: РїСЂРѕРєСЃРёСЂСѓРµС‚ Р±РёР·РЅРµСЃ-РјРµС‚РѕРґС‹ Рё СЂР°СЃСЃС‹Р»Р°РµС‚ СЂРµР·СѓР»СЊС‚Р°С‚ РїРѕРґРїРёСЃС‡РёРєР°Рј.
/// </summary>
internal class Notifier :
    DomainEventNotifier<IPartners, IPartnerAggregate>,
    IPartnerAggregate,
    IObservable<AggregateResult<IPartners, IAnemicModel<IPartners>>>
{
    private readonly List<IObserver<AggregateResult<IPartners, IAnemicModel<IPartners>>>> _observers;

    private Notifier(IPartnerAggregate aggregate, List<IObserver<AggregateResult<IPartners, IAnemicModel<IPartners>>>> observers)
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

    public AggregateResult<IPartners, IAnemicModel<IPartners>> CreatePartner(IAnemicModel<IPartners> model)
        => Aggregate.CreatePartner(model).Do(MatchAndPushNotification);

    public AggregateResult<IPartners, IAnemicModel<IPartners>> UpdatePartner(IAnemicModel<IPartners> model, ICommandToAggregate commandMetadata)
        => Aggregate.UpdatePartner(model, commandMetadata).Do(MatchAndPushNotification);

    public AggregateResult<IPartners, IAnemicModel<IPartners>> DeletePartner(IAnemicModel<IPartners> model, ICommandToAggregate commandMetadata)
        => Aggregate.DeletePartner(model, commandMetadata).Do(MatchAndPushNotification);

    public IDisposable Subscribe(IObserver<AggregateResult<IPartners, IAnemicModel<IPartners>>> observer)
        => Unsubscriber<AggregateResult<IPartners, IAnemicModel<IPartners>>>.Subscribe(_observers, observer);

    private void MatchAndPushNotification(AggregateResult<IPartners, IAnemicModel<IPartners>> result)
    {
        foreach (var observer in _observers)
            observer.OnNext(result);
    }

    internal static IPartnerAggregate Create(
        IPartnerAggregate aggregate,
        List<IObserver<AggregateResult<IPartners, IAnemicModel<IPartners>>>> observers)
        => new Notifier(aggregate, observers);
}
