namespace AppleHealthGateway.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;

/// <summary>Декоратор бизнес-методов агрегата (QG-103): пробрасывает результат наблюдателям.</summary>
public sealed class Notifier : DomainEventNotifier<IAppleHealthGateway, IAppleHealthGatewayAggregate>, IAppleHealthGatewayAggregate,
    IObservable<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>> _observers = new();
    private Notifier(IAppleHealthGatewayAggregate aggregate) : base(aggregate) { }
    public static Notifier Create(IAppleHealthGatewayAggregate aggregate) => new(aggregate);

    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    public IAppleImportRoot Root => Aggregate.Root;
    public IImportConfig ImportConfig => Aggregate.ImportConfig;
    public string LastImport => Aggregate.LastImport;
    public IReadOnlyList<string> ProcessedFiles => Aggregate.ProcessedFiles;
    public AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Configure(Guid clientId, IEnumerable<string> metrics) => Observe(Aggregate.Configure(clientId, metrics));
    public AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> ImportFile(string fileName) => Observe(Aggregate.ImportFile(fileName));

    IDisposable IObservable<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>.Subscribe(IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Observe(AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray()) observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> _observer;
        public Subscription(Notifier notifier, IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>> observer) { _notifier = notifier; _observer = observer; }
        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
