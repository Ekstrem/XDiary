namespace Client.Application.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.Domain.Specifications;
using Client.Application.Abstraction;
using Client.InternalContracts;
using XDiary.Contracts;

public sealed class RegisterClientHandler : IRequestHandler<RegisterClientCommand, RegisterClientResult>, ICommandHandler
{
    private readonly IAggregateProvider<IClients, IClientAggregate> _provider;
    private readonly IObserver<AggregateResult<IClients, IClientAnemicModel>> _observer;
    private readonly IReadRepository<IClients, ClientReadModel> _read;
    private readonly IOtpSender _sender;
    private readonly IOtpRateLimiter _rateLimiter;
    private readonly IConfiguration _config;
    private readonly ILogger<RegisterClientHandler> _log;
    private readonly string? _testOtp;
    private readonly bool _rateLimitDisabled;
    public RegisterClientHandler(IAggregateProvider<IClients, IClientAggregate> provider, IObserver<AggregateResult<IClients, IClientAnemicModel>> observer, IReadRepository<IClients, ClientReadModel> read, IOtpSender sender, IOtpRateLimiter rateLimiter, IConfiguration config, ILogger<RegisterClientHandler> logger)
    { _provider = provider; _observer = observer; _read = read; _sender = sender; _rateLimiter = rateLimiter; _config = config; _log = logger; _testOtp = TestOtpPolicy.Resolve(config, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), out var warn); _rateLimitDisabled = RateLimitPolicy.IsDisabled(config, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")); if (warn) logger.LogWarning("Включён тестовый OTP на проде (Clients:AllowTestOtpOutsideDev=true) — временная мера"); }

    public async Task<RegisterClientResult> Handle(RegisterClientCommand r, CancellationToken ct)
    {
        var channel = _config["Otp:Channel"] ?? "log";
        var phoneCheck = new PhoneRequiredSpecification();
        if (!phoneCheck.IsSatisfiedBy(new AnemicModel { Root = ClientRoot.CreateInstance(r.Phone) }))
            return new RegisterClientResult(ClientResults.Fail(phoneCheck.Reason), false, null, channel, false);
        var phone = PhoneFormatSpecification.Normalize(r.Phone);
        if (!PhoneFormatSpecification.IsValid(phone))
            return new RegisterClientResult(ClientResults.Fail("Номер телефона неверного формата."), false, null, channel, false);
        if (!_rateLimitDisabled && !_rateLimiter.TryAcquire(phone!, DateTime.UtcNow))
            return new RegisterClientResult(ClientResults.Fail("Слишком много запросов, попробуйте позже."), false, null, channel, true);

        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.Phone == phone);

        var otp = _testOtp ?? Random.Shared.Next(100000, 1000000).ToString();
        bool? deliveryOk = null;
        if (_testOtp is null)
        {
            try { deliveryOk = await _sender.SendAsync(phone!, otp, ct); }
            catch (Exception ex) { _log.LogWarning(ex, "OTP send failed for {Phone} (delivery.ok=false).", phone); deliveryOk = false; }
        }

        if (existing is not null)
        {
            var agg = await _provider.GetAggregateAsync(existing.Id, ct);
            ((IObservable<AggregateResult<IClients, IClientAnemicModel>>)agg).Subscribe(_observer);
            var rr = agg.ReissueOtp(otp);
            return new RegisterClientResult(rr, true, deliveryOk, channel, false);
        }

        var newAgg = await _provider.GetAggregateAsync(Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IClients, IClientAnemicModel>>)newAgg).Subscribe(_observer);
        var res = newAgg.Register(phone!, otp);
        return new RegisterClientResult(res, false, deliveryOk, channel, false);
    }
}
public sealed class VerifyClientOtpHandler : IRequestHandler<VerifyClientOtpCommand, AggregateResult<IClients, IClientAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IClients, IClientAggregate> _provider;
    private readonly IObserver<AggregateResult<IClients, IClientAnemicModel>> _observer;
    public VerifyClientOtpHandler(IAggregateProvider<IClients, IClientAggregate> provider, IObserver<AggregateResult<IClients, IClientAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(VerifyClientOtpCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<IClients, IClientAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return ClientResults.Fail("Client not found.");
        var res = aggregate.VerifyOtp(r.Code);
        if (!res.IsSuccess()) aggregate.RecordFailedAttempt();
        return res;
    }
}
public sealed class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, AggregateResult<IClients, IClientAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IClients, IClientAggregate> _provider;
    private readonly IObserver<AggregateResult<IClients, IClientAnemicModel>> _observer;
    public UpdateProfileHandler(IAggregateProvider<IClients, IClientAggregate> provider, IObserver<AggregateResult<IClients, IClientAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(UpdateProfileCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<IClients, IClientAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return ClientResults.Fail("Client not found.");
        return aggregate.UpdateProfile(r.DisplayName, r.Gender, r.PhotoUrl);
    }
}
