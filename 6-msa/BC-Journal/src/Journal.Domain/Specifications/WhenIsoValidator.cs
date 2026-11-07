namespace Journal.Domain.Specifications;

using System.Globalization;

/// <summary>
/// TE-101: startedAt обязан быть датой в формате ISO 8601. Отвергает пустую строку,
/// «не дата» и чистые числа («1234567890») — мусорная дата не должна попадать в сортировку/тренды/CSV.
/// </summary>
internal sealed class WhenIsoValidator
{
    public bool IsSatisfiedBy(string startedAt)
    {
        var t = (startedAt ?? "").Trim();
        return t.Length > 0
            && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
    }

    public string Reason => "startedAt должен быть датой в формате ISO 8601.";
}
