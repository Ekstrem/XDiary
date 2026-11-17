namespace DailyMetrics.Domain.Implementation;
using DailyMetrics.Domain.Abstraction;

// record (positional) — чтобы event-sourced replay мог десериализовать Root из ChangedValueObjectsJson.
public sealed record DailyMetricsRoot(string Date) : IDailyMetricsRoot
{
    public static IDailyMetricsRoot CreateInstance(string date) => new DailyMetricsRoot(date);
}
