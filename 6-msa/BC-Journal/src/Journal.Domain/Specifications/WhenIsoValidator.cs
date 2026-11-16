namespace Journal.Domain.Specifications;

using System.Globalization;

/// <summary>
/// TE-101: startedAt обязан быть датой в формате ISO 8601. Отвергает пустую строку,
/// «не дата» и чистые числа («1234567890»); при успехе отдаёт **DateTimeOffset** (UTC),
/// чтобы мусор не попадал в сортировку/тренды/CSV.
/// </summary>
internal sealed class WhenIsoValidator
{
    public bool TryParse(string startedAt, out DateTimeOffset normalized)
    {
        var t = (startedAt ?? "").Trim();
        if (t.Length > 0
            && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            // TE-101: канонизируем к UTC, чтобы равные моменты давали ОДНО значение.
            normalized = dt.ToUniversalTime();
            return true;
        }
        normalized = default;
        return false;
    }

    public string Reason => "startedAt должен быть датой в формате ISO 8601.";
}
