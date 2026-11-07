namespace Journal.Domain.Specifications;

using System.Globalization;

/// <summary>
/// TE-101: startedAt обязан быть датой в формате ISO 8601. Отвергает пустую строку,
/// «не дата» и чистые числа («1234567890»); при успехе отдаёт **нормализованный** ISO,
/// чтобы мусор не попадал в сортировку/тренды/CSV.
/// </summary>
internal sealed class WhenIsoValidator
{
    public bool TryParse(string startedAt, out string normalized)
    {
        var t = (startedAt ?? "").Trim();
        if (t.Length > 0
            && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            // TE-101: канонизируем к UTC, чтобы равные моменты давали ОДНУ строку (09:00+03:00 == 06:00Z).
            normalized = dt.ToUniversalTime().ToString("o");
            return true;
        }
        normalized = string.Empty;
        return false;
    }

    public string Reason => "startedAt должен быть датой в формате ISO 8601.";
}
