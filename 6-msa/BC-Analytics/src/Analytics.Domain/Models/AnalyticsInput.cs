namespace Analytics.Domain.Models;

/// <summary>
/// Входная запись журнала для аналитики (подмножество полей Record).
/// </summary>
public class RecordInput
{
    public string StartedAt { get; set; } = string.Empty; // "YYYY-MM-DDTHH:mm"
    public int DurationMin { get; set; }
    public int OrgasmsMe { get; set; }
    public int OrgasmsPartner { get; set; }
    public int OrgasmMeRating { get; set; }
    public int OrgasmPartnerRating { get; set; }
}

/// <summary>
/// Входная дневная метрика для аналитики (подмножество полей DailyMetrics).
/// </summary>
public class MetricInput
{
    public string Date { get; set; } = string.Empty; // "YYYY-MM-DD"
    public int RestingHr { get; set; }
    public int SleepScore { get; set; }
}
