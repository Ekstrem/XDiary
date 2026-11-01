namespace Analytics.Api.Precompute;
using Analytics.Application;
using Analytics.Domain.Models;
public sealed class AnalyticsSnapshot
{
    private readonly object _lock = new();
    public SummaryCards Summary30 { get; private set; } = new() { Period = "30" };
    public SummaryCards Summary90 { get; private set; } = new() { Period = "90" };
    public SummaryCards SummaryAll { get; private set; } = new() { Period = "all" };
    public DateTime UpdatedAt { get; private set; }

    public void Update(IReadOnlyList<RecordInput> records)
    {
        lock (_lock)
        {
            Summary30 = SummaryCalculator.Calculate("30", records);
            Summary90 = SummaryCalculator.Calculate("90", records);
            SummaryAll = SummaryCalculator.Calculate("all", records);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public SummaryCards Get(string period) => period switch { "30" => Summary30, "90" => Summary90, _ => SummaryAll };
}