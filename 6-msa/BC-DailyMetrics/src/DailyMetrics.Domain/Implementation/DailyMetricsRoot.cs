namespace DailyMetrics.Domain.Implementation;
using DailyMetrics.Domain.Abstraction;
public sealed class DailyMetricsRoot : IDailyMetricsRoot
{
    public string Date { get; private set; } = string.Empty;
    private DailyMetricsRoot() { }
    public static IDailyMetricsRoot CreateInstance(string date) => new DailyMetricsRoot { Date = date };
}