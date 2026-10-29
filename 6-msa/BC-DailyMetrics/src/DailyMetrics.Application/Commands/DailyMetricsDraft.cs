namespace DailyMetrics.Application.Commands;

/// <summary>
/// Черновик дневной сводки (DailyMetrics).
/// </summary>
public class DailyMetricsDraft
{
    /// <summary>Дата «YYYY-MM-DD».</summary>
    public string Date { get; set; } = string.Empty;
    public int Stress { get; set; }
    public int RestingHr { get; set; }
    public int Hrv { get; set; }
    public int SleepScore { get; set; }
    public int SleepDurationMin { get; set; }
    public int DeepMin { get; set; }
    public int LightMin { get; set; }
    public int RemMin { get; set; }
    public int AwakeMin { get; set; }
}
