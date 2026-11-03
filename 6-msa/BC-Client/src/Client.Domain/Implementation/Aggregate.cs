namespace Client.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Specifications;
using XDiary.Contracts;
public sealed class Aggregate
{
    public IClientAnemicModel Model { get; }
    private Aggregate(IClientAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IClientAnemicModel model) => new(model);

    private AggregateResult<IClients, IClientAnemicModel> Success(AnemicModel m)
    {
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
        var v = new PhoneRequiredValidator();
        var probe = new AnemicModel { Id = Model.Id, Root = ClientRoot.CreateInstance(phone) };
        if (!v.IsSatisfiedBy(probe)) return Fail(v.Reason);
        var m = new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = ClientRoot.CreateInstance(phone), OtpCode = otp, OtpExpiresAt = DateTime.UtcNow.AddMinutes(OtpTtlMinutes) };
        return Success(m);
    }
    public AggregateResult<IClients, IClientAnemicModel> VerifyOtp(string code)
    {
        if (Model.OtpCode == string.Empty) return Fail("Код уже использован.");
        if (Model.OtpExpiresAt is DateTime exp && exp < DateTime.UtcNow) return Fail("Код истёк.");
        if (Model.OtpAttempts >= MaxOtpAttempts) return Fail("Слишком много попыток, попробуйте позже.");
        if (Model.OtpCode != code) return Fail("Неверный код.");
        var m = Clone(); m.Verified = true; m.OtpCode = string.Empty; m.OtpExpiresAt = null; m.OtpAttempts = 0; return Success(m);
    }
    public AggregateResult<IClients, IClientAnemicModel> RecordFailedAttempt()
    {
        var m = Clone(); m.OtpAttempts = Model.OtpAttempts + 1; return Success(m);
    }
    public AggregateResult<IClients, IClientAnemicModel> ReissueOtp(string otp)
    {
        var m = Clone(); m.OtpCode = otp; m.OtpExpiresAt = DateTime.UtcNow.AddMinutes(OtpTtlMinutes); m.OtpAttempts = 0; return Success(m);
    }
    public AggregateResult<IClients, IClientAnemicModel> LinkPartner(Guid partnerId)
    {
        var m = Clone(); m.LinkedPartnerId = partnerId; return Success(m);
    }
    public AggregateResult<IClients, IClientAnemicModel> UnlinkPartner()
    {
        var m = Clone(); m.LinkedPartnerId = null; return Success(m);
    }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, OtpCode = Model.OtpCode, OtpExpiresAt = Model.OtpExpiresAt, OtpAttempts = Model.OtpAttempts, Verified = Model.Verified, LinkedPartnerId = Model.LinkedPartnerId };
}