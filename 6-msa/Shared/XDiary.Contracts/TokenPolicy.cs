namespace XDiary.Contracts;

/// <summary>
/// AR-018: единый источник канонических значений политики токенов (ADR-008/021).
/// Клиент и Privacy обязаны ссылаться сюда, а не дублировать константы —
/// расхождение ловится тестом канонического значения.
/// </summary>
public static class TokenPolicy
{
    public const int AccessMinutes = 15;
    public const int RefreshDays = 30;
    public const int DeviceAccessMinutes = 5; // только Privacy (device)
}
