namespace Client.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Client.Domain;
using Client.Domain.Abstraction;

/// <summary>
/// BE-092: результат регистрации/переотправки OTP.
/// <c>Existing</c> — номер уже зарегистрирован (код переотправлен) → контроллер отдаёт 409.
/// <c>DeliveryOk</c> — null в тестовом режиме (поля `delivery` нет, без ложного «отправлено»).
/// <c>RateLimited</c> — превышен лимит переотправки → контроллер отдаёт 429.
/// </summary>
public sealed record RegisterClientResult(
    AggregateResult<IClients, IClientAnemicModel> Result,
    bool Existing,
    bool? DeliveryOk,
    string Channel,
    bool RateLimited);

public sealed record RegisterClientCommand(string Phone) : IRequest<RegisterClientResult>;
public sealed record VerifyClientOtpCommand(Guid AggregateId, string Code) : IRequest<AggregateResult<IClients, IClientAnemicModel>>;
public sealed record UpdateProfileCommand(Guid AggregateId, string DisplayName, string Gender, string? PhotoUrl) : IRequest<AggregateResult<IClients, IClientAnemicModel>>;
