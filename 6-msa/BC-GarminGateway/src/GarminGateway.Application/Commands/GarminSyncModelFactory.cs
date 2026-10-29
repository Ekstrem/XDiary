using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace GarminGateway.Application.Commands;

/// <summary>
/// Строит Value Objects динамической анемичной модели GarminSync из GarminSyncDraft.
/// </summary>
internal static class GarminSyncModelFactory
{
    public static Dictionary<string, IValueObject> BuildValueObjects(GarminSyncDraft draft) => new()
    {
        ["Root"] = new DynamicValueObject("GarminSyncRoot", new Dictionary<string, object?>
        {
            ["SourceId"] = draft.SourceId
        }),
        ["Credentials"] = new DynamicValueObject("Credentials", new Dictionary<string, object?>
        {
            ["ApiKey"] = draft.ApiKey,
            ["OauthToken"] = draft.OauthToken,
            ["TokenExpiry"] = draft.TokenExpiry
        }),
        ["SyncConfig"] = new DynamicValueObject("SyncConfig", new Dictionary<string, object?>
        {
            ["Metrics"] = draft.Metrics?.ToArray() ?? Array.Empty<string>(),
            ["WindowDays"] = draft.WindowDays
        }),
        ["LastSync"] = new DynamicValueObject("LastSync", new Dictionary<string, object?>
        {
            ["Timestamp"] = draft.LastSync
        })
    };

    /// <summary>Детерминированный Guid из sourceId (один агрегат на источник).</summary>
    public static Guid ToGuid(string sourceId)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sourceId)));
    }

    /// <summary>Инварианты, которые нельзя выразить DSL-метаданных.</summary>
    public static string? Validate(GarminSyncDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.SourceId))
            return "sourceId is required.";
        return null;
    }
}
