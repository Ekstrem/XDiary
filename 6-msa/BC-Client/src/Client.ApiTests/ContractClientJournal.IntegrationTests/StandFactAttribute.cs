using Xunit;

namespace XDiary.Tests.ContractClientJournal;

/// <summary>
/// Требование @TL 2026-11-09 20:40: молчаливый Skip — это ложное зелёное, поэтому
/// отсутствие стенда объявляется **громко**: `::warning::` попадает в лог прогона
/// (GitHub разбирает такие строки как аннотации), плюс отдельный тест печатает сводку
/// «пропущено N проверок».
///
/// Почему не падаем: `ci-backend` поднимает только checkout + dotnet, стенда там нет;
/// красный на отсутствии стенда был бы дефектом гейта, а не находкой.
/// </summary>
public sealed class StandFactAttribute : FactAttribute
{
    public const string BaseUrlVariable = "XDIARY_E2E_BASE_URL";

    /// <summary>Сколько контрактных проверок в наборе — для сводки «пропущено N».</summary>
    public const int ContractChecks = 6;

    public StandFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(BaseUrlVariable)))
        {
            Skip = "[infra] стенд не задан: " + BaseUrlVariable +
                   " — контракт проверяется на dev/E2E-прогоне, не в ci-backend";
            Console.WriteLine(
                "::warning::[infra] контрактные тесты пропущены: стенд не задан (" + BaseUrlVariable +
                ") — проверок не выполнено: " + ContractChecks);
        }
    }
}
