namespace Client.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Client.Domain;
using Client.Domain.Abstraction;

/// <summary>Декоратор бизнес-методов агрегата (QG-103): пробрасывает результат наблюдателям.</summary>
public sealed class Notifier : DomainEventNotifier<IClients, IClientAggregate>, IClientAggregate,
    IObservable<AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly List<IObserver<AggregateResult<IClients, IClientAnemicModel>>> _observers = new();
    private Notifier(IClientAggregate aggregate) : base(aggregate) { }
    public static Notifier Create(IClientAggregate aggregate) => new(aggregate);

    public Guid Id => Aggregate.Id;
    public long Version => Aggregate.Version;
    public Guid CorrelationToken => Aggregate.CorrelationToken;
    public string CommandName => Aggregate.CommandName;
    public string SubjectName => Aggregate.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Aggregate.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Aggregate.GetValueObjects();

    public IClientRoot Root => Aggregate.Root;
    public string DisplayName => Aggregate.DisplayName;
    public string Gender => Aggregate.Gender;
    public string? PhotoUrl => Aggregate.PhotoUrl;
    public string OtpCode => Aggregate.OtpCode;
    public DateTime? OtpExpiresAt => Aggregate.OtpExpiresAt;
    public int OtpAttempts => Aggregate.OtpAttempts;
    public bool Verified => Aggregate.Verified;

    public AggregateResult<IClients, IClientAnemicModel> Register(string phone, string otp) => Observe(Aggregate.Register(phone, otp));
    public AggregateResult<IClients, IClientAnemicModel> VerifyOtp(string code) => Observe(Aggregate.VerifyOtp(code));
    public AggregateResult<IClients, IClientAnemicModel> RecordFailedAttempt() => Observe(Aggregate.RecordFailedAttempt());
    public AggregateResult<IClients, IClientAnemicModel> UpdateProfile(string displayName, string gender, string? photoUrl) => Observe(Aggregate.UpdateProfile(displayName, gender, photoUrl));
    public AggregateResult<IClients, IClientAnemicModel> ReissueOtp(string otp) => Observe(Aggregate.ReissueOtp(otp));

    IDisposable IObservable<AggregateResult<IClients, IClientAnemicModel>>.Subscribe(IObserver<AggregateResult<IClients, IClientAnemicModel>> observer)
    {
        _observers.Add(observer);
        return new Subscription(this, observer);
    }

    private AggregateResult<IClients, IClientAnemicModel> Observe(AggregateResult<IClients, IClientAnemicModel> result)
    {
        foreach (var observer in _observers.ToArray()) observer.OnNext(result);
        return result;
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Notifier _notifier;
        private readonly IObserver<AggregateResult<IClients, IClientAnemicModel>> _observer;
        public Subscription(Notifier notifier, IObserver<AggregateResult<IClients, IClientAnemicModel>> observer) { _notifier = notifier; _observer = observer; }
        public void Dispose() => _notifier._observers.Remove(_observer);
    }
}
