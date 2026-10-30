namespace DailyMetrics.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ISleep : IValueObject
{
    int Score { get; }
    int DurationMin { get; }
    int DeepMin { get; }
    int LightMin { get; }
    int RemMin { get; }
    int AwakeMin { get; }
}