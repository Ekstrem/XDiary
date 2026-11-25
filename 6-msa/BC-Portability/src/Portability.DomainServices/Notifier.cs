namespace Portability.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;

/// <summary>Декоратор бизнес-методов агрегата (QG-103): пробрасывает результат наблюдателям.</summary>
public sealed class Notifier : DomainEventNotifier<IPortability, ICsvDocumentAggregate>, ICsvDocumentAggregate,
    IObservable<AggregateResult<IPortability, ICsvDocumentAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>>> _observers = new();
    private Notifier(ICsvDocumentAggregate aggregate) : base(aggregate) { }
    public static Notifier Create(ICsvDocumentAggregate aggregate) => new(aggregate);

    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    public ICsvDocumentRoot Root => Aggregate.Root;
    public IReadOnlyList<string> Rows => Aggregate.Rows;
    public string Kind => Aggregate.Kind;
    public Guid? ClientId => Aggregate.ClientId;
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ExportRecords(IReadOnlyList<string> rows, Guid clientId, CommandToAggregate? command = null) => Observe(Aggregate.ExportRecords(rows, clientId, command));
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportRecords(IReadOnlyList<string> rows, Guid clientId, CommandToAggregate? command = null) => Observe(Aggregate.ImportRecords(rows, clientId, command));
    public AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportSomatic(IReadOnlyList<string> rows, Guid clientId, CommandToAggregate? command = null) => Observe(Aggregate.ImportSomatic(rows, clientId, command));

    IDisposable IObservable<AggregateResult<IPortability, ICsvDocumentAnemicModel>>.Subscribe(IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<IPortability, ICsvDocumentAnemicModel> Observe(AggregateResult<IPortability, ICsvDocumentAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray()) observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>> _observer;
        public Subscription(Notifier notifier, IObserver<AggregateResult<IPortability, ICsvDocumentAnemicModel>> observer) { _notifier = notifier; _observer = observer; }
        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
