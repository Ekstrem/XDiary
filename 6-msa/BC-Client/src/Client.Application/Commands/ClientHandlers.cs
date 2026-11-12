namespace Client.Application.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.Domain.Specifications;
using Client.Application.Abstraction;
using Client.InternalContracts;
using XDiary.Contracts;

public sealed class RegisterClientHandler : IRequestHandler<RegisterClientCommand, RegisterClientResult>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    private readonly IClientQueryRepository _query;
    private readonly IOtpSender _sender;
    private readonly IOtpRateLimiter _rateLimiter;
    private readonly IConfiguration _config;
    private readonly ILogger<RegisterClientHandler> _log;
    private readonly string? _testOtp;
    public RegisterClientHandler(IRepository<IClients, IClientAnemicModel> repo, IClientQueryRepository query, IOtpSender sender, IOtpRateLimiter rateLimiter, IConfiguration config, ILogger<RegisterClientHandler> logger) { _repo = repo; _query = query; _sender = sender; _rateLimiter = rateLimiter; _config = config; _log = logger; _testOtp = TestOtpPolicy.Resolve(config, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), out var warn); if (warn) logger.LogWarning("Включён тестовый OTP на проде (Clients:AllowTestOtpOutsideDev=true) — временная мера"); }

    public async Task<RegisterClientResult> Handle(RegisterClientCommand r, CancellationToken ct)
    {
        var channel = _config["Otp:Channel"] ?? "log";

        // BE-092 (@TL 08:02): валидация телефона ДО отправки — иначе шлём OTP на пустой номер
        // («Telegram OTP отправлен на .» на проде).
        var phoneCheck = new PhoneRequiredValidator();
        if (!phoneCheck.IsSatisfiedBy(new AnemicModel { Root = ClientRoot.CreateInstance(r.Phone) }))
            return new RegisterClientResult(ClientResults.Fail(phoneCheck.Reason), false, null, channel, false);

        // TE-097 (канон AR-025): нормализация и проверка ФОРМАТА — до лимитера и отправки OTP.
        // «abc»/«123» проходят PhoneRequiredValidator (он про пустоту), поэтому без этой
        // проверки OTP уходит на невалидный номер. Уникальность считается по нормализованной форме.
        var phone = PhoneFormatValidator.Normalize(r.Phone);
        if (!PhoneFormatValidator.IsValid(phone))
            return new RegisterClientResult(ClientResults.Fail("Номер телефона неверного формата."), false, null, channel, false);

        // BE-092: лимит переотправки — до отправки, иначе OTP-бомбинг.
        if (!_rateLimiter.TryAcquire(phone!, DateTime.UtcNow))
            return new RegisterClientResult(ClientResults.Fail("Слишком много запросов, попробуйте позже."), false, null, channel, true);

        // BE-092 (@TL 08:02): существующий номер — тоже ДО отправки (не шлём повторный код, пока не знаем номера).
        var existing = await _query.GetByPhoneAsync(phone!, ct);

        var otp = _testOtp ?? Random.Shared.Next(100000, 1000000).ToString();
        bool? deliveryOk = null;
        if (_testOtp is null)
        {
            // BE-092: недоступный канал — штатное delivery.ok=false, а не отказ регистрации.
            try { deliveryOk = await _sender.SendAsync(phone!, otp, ct); }
            catch (Exception ex) { _log.LogWarning(ex, "OTP send failed for {Phone} (delivery.ok=false).", phone); deliveryOk = false; }
        }

        if (existing is not null)
        {
            var agg = await _repo.GetByIdAsync(existing.Id, ct);
            if (agg is not null)
            {
                var rr = Aggregate.CreateInstance(agg).ReissueOtp(otp);
                if (rr.IsSuccess())
                {
                    try { await _repo.SaveAsync(rr, ct); }
                    catch (DuplicatePhoneException ex) { return new RegisterClientResult(ClientResults.Fail(ex.Message), false, deliveryOk, channel, false); }
                }
                return new RegisterClientResult(rr, true, deliveryOk, channel, false);
            }
        }

        var res = Aggregate.CreateInstance(new AnemicModel()).Register(phone!, otp);
        if (res.IsSuccess())
        {
            try { await _repo.SaveAsync(res, ct); }
            catch (DuplicatePhoneException)
            {
                // BE-092 (@DO 15:48): гонка — GetByPhoneAsync не увидел строку, INSERT дал 23505.
                // Отдаём 409 + clientId (переотправка), как путь Existing = true, а не 400/200.
                var raced = await _query.GetByPhoneAsync(r.Phone, ct);
                if (raced is not null)
                {
                    var racedAgg = await _repo.GetByIdAsync(raced.Id, ct);
                    if (racedAgg is not null)
                    {
                        var rr = Aggregate.CreateInstance(racedAgg).ReissueOtp(otp);
                        if (rr.IsSuccess()) { try { await _repo.SaveAsync(rr, ct); } catch (DuplicatePhoneException) { } }
                        return new RegisterClientResult(rr, true, deliveryOk, channel, false);
                    }
                }
                return new RegisterClientResult(ClientResults.Fail("phone_taken"), false, deliveryOk, channel, false);
            }
        }
        return new RegisterClientResult(res, false, deliveryOk, channel, false);
    }
}
public sealed class VerifyClientOtpHandler : IRequestHandler<VerifyClientOtpCommand, AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    public VerifyClientOtpHandler(IRepository<IClients, IClientAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(VerifyClientOtpCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return ClientResults.Fail("Client not found."); var res = Aggregate.CreateInstance(m).VerifyOtp(r.Code); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); else { var fail = Aggregate.CreateInstance(m).RecordFailedAttempt(); await _repo.SaveAsync(fail, ct); } return res; }
}
public sealed class UpdateProfileHandler : IRequestHandler<UpdateProfileCommand, AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    public UpdateProfileHandler(IRepository<IClients, IClientAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(UpdateProfileCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return ClientResults.Fail("Client not found."); var res = Aggregate.CreateInstance(m).UpdateProfile(r.DisplayName, r.Gender, r.PhotoUrl); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
