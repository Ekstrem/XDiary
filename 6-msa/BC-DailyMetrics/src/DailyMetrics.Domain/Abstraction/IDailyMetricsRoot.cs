namespace DailyMetrics.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IDailyMetricsRoot : IValueObject { string Date { get; } }