namespace Catalog.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;

/// <summary>Декоратор бизнес-методов агрегата (QG-103): пробрасывает результат наблюдателям.</summary>
public sealed class Notifier : DomainEventNotifier<ICatalog, ICatalogAggregate>, ICatalogAggregate,
    IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>>> _observers = new();
    private Notifier(ICatalogAggregate aggregate) : base(aggregate) { }
    public static Notifier Create(ICatalogAggregate aggregate) => new(aggregate);

    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    public ICatalogItemRoot Root => Aggregate.Root;
    public string Photo => Aggregate.Photo;
    public string Notes => Aggregate.Notes;
    public string Brand => Aggregate.Brand;
    public bool Archived => Aggregate.Archived;
    public DateTime? DeletedAt => Aggregate.DeletedAt;
    public string FantasyPartner => Aggregate.FantasyPartner;
    public Guid? ClientId => Aggregate.ClientId;

    public AggregateResult<ICatalog, ICatalogAnemicModel> AddItem(string kind, string name, Guid clientId, CommandToAggregate? command = null) => Observe(Aggregate.AddItem(kind, name, clientId, command));
    public AggregateResult<ICatalog, ICatalogAnemicModel> Rename(string name, CommandToAggregate? command = null) => Observe(Aggregate.Rename(name, command));
    public AggregateResult<ICatalog, ICatalogAnemicModel> UpdateDetails(string photo, string notes, string brand, string fantasyPartner, CommandToAggregate? command = null) => Observe(Aggregate.UpdateDetails(photo, notes, brand, fantasyPartner, command));
    public AggregateResult<ICatalog, ICatalogAnemicModel> Archive(CommandToAggregate? command = null) => Observe(Aggregate.Archive(command));
    public AggregateResult<ICatalog, ICatalogAnemicModel> Restore(CommandToAggregate? command = null) => Observe(Aggregate.Restore(command));
    public AggregateResult<ICatalog, ICatalogAnemicModel> Remove(CommandToAggregate? command = null) => Observe(Aggregate.Remove(command));

    IDisposable IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>.Subscribe(IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<ICatalog, ICatalogAnemicModel> Observe(AggregateResult<ICatalog, ICatalogAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray()) observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
        public Subscription(Notifier notifier, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _notifier = notifier; _observer = observer; }
        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
