using Xunit;

namespace XDiary.Tests.GatewaysApi;

/// <summary>
/// TE-040: поведения шлюза проверяются против **поднятого стенда** (вариант C: сквозные
/// поведения на dev, внутренности сервиса — в BE-044).
///
/// Без `XDIARY_E2E_BASE_URL` — **громкий** пропуск (правило @TL «молчаливый Skip = ложное
/// зелёное»): `::warning::` в лог + причина в `Skip`.
/// </summary>
public sealed class StandFactAttribute : FactAttribute
{
    public const string BaseUrlVariable = "XDIARY_E2E_BASE_URL";
    public const int Behaviors = 8;

    public StandFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BaseUrlVariable)))
        {
            Skip = "[infra] стенд не задан: " + BaseUrlVariable +
                   " — поведения шлюза проверяются на dev/E2E-прогоне";
            Console.WriteLine(
                "::warning::[infra] тесты шлюза пропущены: стенд не задан (" + BaseUrlVariable +
                ") — проверок не выполнено: " + Behaviors);
        }
    }
}
