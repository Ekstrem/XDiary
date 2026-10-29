using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Portability.Application.Commands;

/// <summary>
/// Строит Value Objects динамической анемичной модели CsvDocument из CsvDocumentDraft.
/// </summary>
internal static class CsvDocumentModelFactory
{
    public static Dictionary<string, IValueObject> BuildValueObjects(CsvDocumentDraft draft) => new()
    {
        ["Root"] = new DynamicValueObject("CsvDocumentRoot", new Dictionary<string, object?>
        {
            ["SourceId"] = draft.SourceId
        }),
        ["Headers"] = new DynamicValueObject("CsvHeaders", new Dictionary<string, object?>
        {
            ["Headers"] = draft.Headers?.ToArray() ?? Array.Empty<string>()
        }),
        ["Rows"] = new DynamicValueObject("CsvRows", new Dictionary<string, object?>
        {
            ["Rows"] = draft.Rows?.ToArray() ?? Array.Empty<string>()
        })
    };

    /// <summary>Детерминированный Guid из sourceId (один документ на тип).</summary>
    public static Guid ToGuid(string sourceId)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sourceId)));
    }

    /// <summary>Инварианты, которые нельзя выразить DSL-метаданных.</summary>
    public static string? Validate(CsvDocumentDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.SourceId))
            return "sourceId is required.";
        return null;
    }
}
