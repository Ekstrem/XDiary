using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace AppleHealthGateway.Application.Commands;

/// <summary>
/// Строит Value Objects динамической анемичной модели AppleImport из AppleImportDraft.
/// </summary>
internal static class AppleImportModelFactory
{
    public static Dictionary<string, IValueObject> BuildValueObjects(AppleImportDraft draft) => new()
    {
        ["Root"] = new DynamicValueObject("AppleImportRoot", new Dictionary<string, object?>
        {
            ["SourceId"] = draft.SourceId
        }),
        ["ImportConfig"] = new DynamicValueObject("ImportConfig", new Dictionary<string, object?>
        {
            ["Metrics"] = draft.Metrics?.ToArray() ?? Array.Empty<string>()
        }),
        ["LastImport"] = new DynamicValueObject("LastImport", new Dictionary<string, object?>
        {
            ["Timestamp"] = draft.LastImport
        }),
        ["ProcessedFiles"] = new DynamicValueObject("ProcessedFiles", new Dictionary<string, object?>
        {
            ["Names"] = draft.ProcessedFiles?.ToArray() ?? Array.Empty<string>()
        })
    };

    /// <summary>Детерминированный Guid из sourceId (один агрегат на источник).</summary>
    public static Guid ToGuid(string sourceId)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sourceId)));
    }

    /// <summary>Инварианты, которые нельзя выразить DSL-метаданных.</summary>
    public static string? Validate(AppleImportDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.SourceId))
            return "sourceId is required.";
        return null;
    }
}
