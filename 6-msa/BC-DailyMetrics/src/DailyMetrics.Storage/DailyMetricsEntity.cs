namespace DailyMetrics.Storage;
public sealed class DailyMetricsEntity
{
    public Guid Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public int Stress { get; set; }
    public int RestingHr { get; set; }
    public int Hrv { get; set; }
    public int SleepScore { get; set; }
    public int SleepDurationMin { get; set; }
    public int SleepDeepMin { get; set; }
    public int SleepLightMin { get; set; }
    public int SleepRemMin { get; set; }
    public int SleepAwakeMin { get; set; }
    public long Version { get; set; }
}