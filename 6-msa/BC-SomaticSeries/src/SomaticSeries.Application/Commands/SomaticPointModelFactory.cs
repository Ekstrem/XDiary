using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace SomaticSeries.Application.Commands;

/// <summary>
/// Строит Value Objects динамической анемичной модели SomaticPoint из SomaticPointDraft.
/// </summary>
internal static class SomaticPointModelFactory
{
    public static Dictionary<string, IValueObject> BuildValueObjects(SomaticPointDraft draft) => new()
    {
        ["Root"] = new DynamicValueObject("SomaticPointRoot", new Dictionary<string, object?>
        {
            ["T"] = draft.T
        }),
        ["HeartRate"] = new DynamicValueObject("HeartRate", new Dictionary<string, object?>
        {
            ["Hr"] = draft.Hr
        }),
        ["BodyBattery"] = new DynamicValueObject("BodyBattery", new Dictionary<string, object?>
        {
            ["BodyBattery"] = draft.BodyBattery
        })
    };

    /// <summary>Детерминированный Guid из минутной метки t (для upsert по t).</summary>
    public static Guid ToGuid(string t)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(t)));
    }

    /// <summary>Инварианты, которые нельзя выразить DSL-метаданных (диапазоны).</summary>
    public static string? Validate(SomaticPointDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.T))
            return "t is required.";
        if (draft.Hr < 0 || draft.BodyBattery < 0)
            return "hr and body_battery must be >= 0.";
        return null;
    }
}
