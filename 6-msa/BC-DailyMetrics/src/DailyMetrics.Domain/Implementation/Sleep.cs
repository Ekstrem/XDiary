namespace DailyMetrics.Domain.Implementation;
using DailyMetrics.Domain.Abstraction;
public sealed class Sleep : ISleep
{
    public int Score { get; private set; }
    public int DurationMin { get; private set; }
    public int DeepMin { get; private set; }
    public int LightMin { get; private set; }
    public int RemMin { get; private set; }
    public int AwakeMin { get; private set; }
    private Sleep() { }
    public static ISleep CreateInstance(int score, int durationMin, int deepMin, int lightMin, int remMin, int awakeMin)
        => new Sleep { Score = score, DurationMin = durationMin, DeepMin = deepMin, LightMin = lightMin, RemMin = remMin, AwakeMin = awakeMin };
}