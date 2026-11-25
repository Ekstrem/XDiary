namespace Journal.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;

/// <summary>
/// QG-103: Notifier — декоратор бизнес-методов агрегата. Реализует IRecordAggregate (т.е. IAggregate<IJournal>)
/// и пробрасывает результат каждого метода наблюдателям (IObservable) через реактивную обвязку.
/// </summary>
public sealed class Notifier : DomainEventNotifier<IJournal, IRecordAggregate>, IRecordAggregate,
    IObservable<AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<IJournal, IJournalAnemicModel>>> _observers = new();

    // QG-005: агрегат — приватный конструктор + статическая фабрика Create.
    private Notifier(IRecordAggregate aggregate) : base(aggregate) { }

    public static Notifier Create(IRecordAggregate aggregate) => new(aggregate);

    // IAggregate<IJournal>:
    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    // IJournalAnemicModel:
    public IRecordRoot Root => Aggregate.Root;
    public Guid? ClientId => Aggregate.ClientId;
    public int DurationMin => Aggregate.DurationMin;
    public string Place => Aggregate.Place;
    public IReadOnlyList<string> Positions => Aggregate.Positions;
    public IReadOnlyList<string> Costumes => Aggregate.Costumes;
    public IReadOnlyList<string> Toys => Aggregate.Toys;
    public IReadOnlyList<string> Fantasies => Aggregate.Fantasies;
    public IReadOnlyList<string> Tags => Aggregate.Tags;
    public IReadOnlyList<string> Contraceptives => Aggregate.Contraceptives;
    public int ContraceptiveCount => Aggregate.ContraceptiveCount;
    public IReadOnlyList<IOrgasms> Orgasms => Aggregate.Orgasms;
    public bool Garmin => Aggregate.Garmin;
    public string GarminId => Aggregate.GarminId;
    public string CreatedAt => Aggregate.CreatedAt;
    public string UpdatedAt => Aggregate.UpdatedAt;
    public RecordState State => Aggregate.State;
    public bool Deleted => Aggregate.Deleted;

    // Бизнес-операции (13): каждая пробрасывает результат наблюдателям.
    public AggregateResult<IJournal, IJournalAnemicModel> StartManualEntry(Guid clientId, CommandToAggregate command)
        => Observe(Aggregate.StartManualEntry(clientId, command));
    public AggregateResult<IJournal, IJournalAnemicModel> StartFromGarmin(string startedAt, int durationMin, string garminId, Guid clientId, CommandToAggregate command)
        => Observe(Aggregate.StartFromGarmin(startedAt, durationMin, garminId, clientId, command));
    public AggregateResult<IJournal, IJournalAnemicModel> SpecifyWhen(string startedAt, CommandToAggregate command)
        => Observe(Aggregate.SpecifyWhen(startedAt, command));
    public AggregateResult<IJournal, IJournalAnemicModel> AddPartner(string partnerId, CommandToAggregate command)
        => Observe(Aggregate.AddPartner(partnerId, command));
    public AggregateResult<IJournal, IJournalAnemicModel> RemovePartner(string partnerId, CommandToAggregate command)
        => Observe(Aggregate.RemovePartner(partnerId, command));
    public AggregateResult<IJournal, IJournalAnemicModel> SpecifyPlace(string place, CommandToAggregate command)
        => Observe(Aggregate.SpecifyPlace(place, command));
    public AggregateResult<IJournal, IJournalAnemicModel> AddPositions(IEnumerable<string> positions, CommandToAggregate command)
        => Observe(Aggregate.AddPositions(positions, command));
    public AggregateResult<IJournal, IJournalAnemicModel> AddAccessories(IEnumerable<string> costumes, IEnumerable<string> toys, IEnumerable<string> fantasies, IEnumerable<string> tags, CommandToAggregate command)
        => Observe(Aggregate.AddAccessories(costumes, toys, fantasies, tags, command));
    public AggregateResult<IJournal, IJournalAnemicModel> RecordContraception(IEnumerable<string> contraceptives, int count, CommandToAggregate command)
        => Observe(Aggregate.RecordContraception(contraceptives, count, command));
    public AggregateResult<IJournal, IJournalAnemicModel> SpecifyDuration(int durationMin, CommandToAggregate command)
        => Observe(Aggregate.SpecifyDuration(durationMin, command));
    public AggregateResult<IJournal, IJournalAnemicModel> RecordOrgasms(IEnumerable<IOrgasms> orgasms, CommandToAggregate command)
        => Observe(Aggregate.RecordOrgasms(orgasms, command));
    public AggregateResult<IJournal, IJournalAnemicModel> Complete(CommandToAggregate command)
        => Observe(Aggregate.Complete(command));
    public AggregateResult<IJournal, IJournalAnemicModel> Delete(CommandToAggregate command)
        => Observe(Aggregate.Delete(command));

    // Явная реализация IObservable: Subscribe — контракт IObservable, а не бизнес-метод агрегата (QG-007 его не трогает).
    IDisposable IObservable<AggregateResult<IJournal, IJournalAnemicModel>>.Subscribe(IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<IJournal, IJournalAnemicModel> Observe(AggregateResult<IJournal, IJournalAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray())
            observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;

        public Subscription(Notifier notifier, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer)
        {
            _notifier = notifier;
            _observer = observer;
        }

        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
