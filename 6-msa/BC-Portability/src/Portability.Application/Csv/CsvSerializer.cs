namespace Portability.Application.Csv;
public static class CsvSerializer
{
    public static string Serialize(IReadOnlyList<string> headers, IReadOnlyList<string> rows)
        => string.Join(",", headers) + "\n" + string.Join("\n", rows);
    public static IReadOnlyList<string> Deserialize(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
        return csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.TrimEnd('\r')).ToList();
    }
}