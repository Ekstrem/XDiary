namespace Client.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Specifications;
using XDiary.Contracts;
internal sealed class Aggregate : IClientAggregate
{
    public IClientAnemicModel Model { get; }
    private Aggregate(IClientAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IClientAnemicModel model) => new(model);

    // IAggregate<IClients> / IAnemicModel<IClients>:
    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    // IClientAnemicModel:
    public IClientRoot Root => Model.Root;
    public string DisplayName => Model.DisplayName;
    public string Gender => Model.Gender;
    public string? PhotoUrl => Model.PhotoUrl;
    public string OtpCode => Model.OtpCode;
    public DateTime? OtpExpiresAt => Model.OtpExpiresAt;
    public int OtpAttempts => Model.OtpAttempts;
    public bool Verified => Model.Verified;

    private AggregateResult<IClients, IClientAnemicModel> Success(AnemicModel m, [System.Runtime.CompilerServices.CallerMemberName] string commandName = "")
    {
        var command = CommandToAggregate.Commit(m.CorrelationToken, commandName, m.SubjectName, Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Model.Version + 1));
        m = m with { Version = command.Version, CommandName = command.CommandName, SubjectName = command.SubjectName }; // BE-150: Command.Version = новая версия агрегата (timestamp), acted-on Version остаётся в DomainEvent.Version
        var data = BusinessOperationData<IClients, IClientAnemicModel>.Commit<IClients, IClientAnemicModel>(Model, m);
        return new AggregateResultSuccess<IClients, IClientAnemicModel>(data);
    }
    private AggregateResult<IClients, IClientAnemicModel> Fail(string e)
    {
        var data = BusinessOperationData<IClients, IClientAnemicModel>.Commit<IClients, IClientAnemicModel>(Model, Model);
        return new AggregateResultException<IClients, IClientAnemicModel>(data, new FailedSpecification<IClients, IClientAnemicModel>(e));
    }

    private const int MaxOtpAttempts = 5;
    private const int OtpTtlMinutes = 5;

    public AggregateResult<IClients, IClientAnemicModel> Register(string phone, string otp)
    {
        var v = new PhoneRequiredSpecification();
        var probe = new AnemicModel { Id = Model.Id, Root = ClientRoot.CreateInstance(phone) };
        if (!v.IsSatisfiedBy(probe)) return Fail(v.Reason);
        var m = new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = ClientRoot.CreateInstance(phone), OtpCode = otp, OtpExpiresAt = DateTime.UtcNow.AddMinutes(OtpTtlMinutes) };
        return Success(m);
    }
    public AggregateResult<IClients, IClientAnemicModel> VerifyOtp(string code)
    {
        if (Model.OtpCode == string.Empty) return Fail("Код уже использован.");
        if (Model.OtpExpiresAt is null || Model.OtpExpiresAt.Value < DateTime.UtcNow) return Fail("Код истёк.");
        if (Model.OtpAttempts >= MaxOtpAttempts) return Fail("Слишком много попыток, попробуйте позже.");
        if (Model.OtpCode != code) return Fail("Неверный код.");
        return Success(Clone() with { Verified = true, OtpCode = string.Empty, OtpExpiresAt = null, OtpAttempts = 0 });
    }
    public AggregateResult<IClients, IClientAnemicModel> RecordFailedAttempt()
    {
        return Success(Clone() with { OtpAttempts = Model.OtpAttempts + 1 });
    }
    public AggregateResult<IClients, IClientAnemicModel> UpdateProfile(string displayName, string gender, string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return Fail("displayName обязателен.");
        var g = (gender ?? "").Trim().ToLowerInvariant();
        if (g is not ("male" or "female")) return Fail("gender: допустимо male или female.");
        return Success(Clone() with { DisplayName = displayName, Gender = g, PhotoUrl = photoUrl });
    }
    public AggregateResult<IClients, IClientAnemicModel> ReissueOtp(string otp)
    {
        return Success(Clone() with { OtpCode = otp, OtpExpiresAt = DateTime.UtcNow.AddMinutes(OtpTtlMinutes), OtpAttempts = 0 });
    }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, OtpCode = Model.OtpCode, OtpExpiresAt = Model.OtpExpiresAt, OtpAttempts = Model.OtpAttempts, DisplayName = Model.DisplayName, Gender = Model.Gender, PhotoUrl = Model.PhotoUrl, Verified = Model.Verified };
}
