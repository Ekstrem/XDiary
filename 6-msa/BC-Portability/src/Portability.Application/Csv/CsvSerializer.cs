using System.Text;

namespace Portability.Application.Csv;

/// <summary>
/// Сериализация/десериализация CSV (Published Language).
/// Заголовки фиксированы (CSV_HEADERS), списки полей — через `;`.
/// </summary>
public static class CsvSerializer
{
    public const char ListSeparator = ';';

    public static string Serialize(IReadOnlyList<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', CsvHeaders.Record));
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', row.Select(Escape)));
        return sb.ToString();
    }

    public static (string[] Headers, List<string[]> Rows) Deserialize(string csv)
    {
        var lines = csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            return (Array.Empty<string>(), new List<string[]>());

        var headers = lines[0].Split(',').Select(Unescape).ToArray();
        var rows = lines.Skip(1)
            .Select(l => l.Split(',').Select(Unescape).ToArray())
            .ToList();
        return (headers, rows);
    }

    public static bool HasValidHeaders(string[] headers)
        => headers.Length == CsvHeaders.Record.Length && headers.SequenceEqual(CsvHeaders.Record);

    public static string JoinList(IEnumerable<string> items)
        => string.Join(ListSeparator, items);

    public static List<string> SplitList(string value)
        => value.Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();

    private static string Escape(string field)
        => field.Contains(',') || field.Contains('"')
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;

    private static string Unescape(string field)
        => field.Length >= 2 && field.StartsWith('"') && field.EndsWith('"')
            ? field[1..^1].Replace("\"\"", "\"")
            : field;
}
