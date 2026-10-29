using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace DailyMetrics.Application.Commands;

/// <summary>
/// Строит Value Objects динамической анемичной модели DailyMetrics из DailyMetricsDraft.
/// </summary>
internal static class DailyMetricsModelFactory
{
    public static Dictionary<string, IValueObject> BuildValueObjects(DailyMetricsDraft draft) => new()
    {
        ["Root"] = new DynamicValueObject("DailyMetricsRoot", new Dictionary<string, object?>
        {
            ["Date"] = draft.Date
        }),
        ["Stress"] = new DynamicValueObject("Stress", new Dictionary<string, object?>
        {
            ["Stress"] = draft.Stress
        }),
        ["RestingHR"] = new DynamicValueObject("RestingHR", new Dictionary<string, object?>
        {
            ["RestingHr"] = draft.RestingHr
        }),
        ["Hrv"] = new DynamicValueObject("Hrv", new Dictionary<string, object?>
        {
            ["Hrv"] = draft.Hrv
        }),
        ["Sleep"] = new DynamicValueObject("Sleep", new Dictionary<string, object?>
        {
            ["Score"] = draft.SleepScore,
            ["DurationMin"] = draft.SleepDurationMin,
            ["DeepMin"] = draft.DeepMin,
            ["LightMin"] = draft.LightMin,
            ["RemMin"] = draft.RemMin,
            ["AwakeMin"] = draft.AwakeMin
        })
    };

    /// <summary>Детерминированный Guid из даты (для upsert по date).</summary>
    public static Guid ToGuid(string date)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(date)));
    }

    /// <summary>Инварианты, которые нельзя выразить DSL-метаданных (диапазоны).</summary>
    public static string? Validate(DailyMetricsDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Date))
            return "date is required.";
        if (draft.Stress < 0 || draft.RestingHr < 0 || draft.Hrv < 0)
            return "stress, resting_hr and hrv must be >= 0.";
        if (draft.SleepScore < 0 || draft.SleepDurationMin < 0 || draft.DeepMin < 0 || draft.LightMin < 0 || draft.RemMin < 0 || draft.AwakeMin < 0)
            return "sleep metrics must be >= 0.";
        return null;
    }
}
