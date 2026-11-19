namespace DailyMetrics.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IDailyMetricsRoot : IAggregateRoot<IDailyMetrics> { string Date { get; } }