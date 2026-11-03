using Xunit;

namespace XDiary.Tests.ContractClientJournal;

/// <summary>
/// Требование @TL 2026-11-09 20:40 п.1: в логе должно быть **видно**, что контракт не проверялся.
/// Атрибут печатает `::warning::` при discovery, но если раннер проглотит вывод discovery —
/// эта всегда-зелёная проверка скажет то же самое явно, с числом пропущенных проверок.
///
/// «Проверено 0» и «не проверено» — разные вещи; молчаливый Skip их смешивает.
/// </summary>
[Trait("Area", "Contract.ClientJournal")]
[Trait("Task", "TE-038")]
public sealed class SuiteSummaryTests
{
    [Fact]
    public void Summary_States_Whether_Contract_Was_Checked()
    {
        var baseUrl = Environment.GetEnvironmentVariable(StandFactAttribute.BaseUrlVariable);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Console.WriteLine(
                "::warning::[infra] контрактные тесты пропущены: стенд не задан (" +
                StandFactAttribute.BaseUrlVariable + ") — пропущено проверок: " +
                StandFactAttribute.ContractChecks);
            // Зелёный, но не тихий: строка выше делает пропуск наблюдаемым в логе прогона.
            return;
        }

        Console.WriteLine(
            "контракт проверяется на стенде " + baseUrl +
            " — проверок в наборе: " + StandFactAttribute.ContractChecks);
    }
}
