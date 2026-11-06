namespace Client.Application.Abstraction;
/// <summary>
/// BE-092: возвращает `true`, если провайдер подтвердил доставку; `false` — попытка была,
/// но канал недоступен/не подтвердил. Не бросает исключения на «канал недоступен» —
/// это штатное состояние `delivery.ok=false`, а не отказ операции.
/// </summary>
public interface IOtpSender
{
    Task<bool> SendAsync(string phone, string otp, CancellationToken ct = default);
}
