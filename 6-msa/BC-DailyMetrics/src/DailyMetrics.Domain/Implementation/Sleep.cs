namespace DailyMetrics.Domain.Implementation;
using DailyMetrics.Domain.Abstraction;

// record (positional) — value object с getter-only свойствами.
public sealed record Sleep(int Score, int DurationMin, int DeepMin, int LightMin, int RemMin, int AwakeMin) : ISleep
{
    public static ISleep CreateInstance(int score, int durationMin, int deepMin, int lightMin, int remMin, int awakeMin)
        => new Sleep(score, durationMin, deepMin, lightMin, remMin, awakeMin);
}
