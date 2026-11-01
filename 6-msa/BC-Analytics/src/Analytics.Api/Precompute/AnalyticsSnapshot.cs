namespace Analytics.Api.Precompute;
using Analytics.Domain.Models;
public sealed class AnalyticsSnapshot
{
    private readonly object _lock = new();
    public SummaryCards Summary30 { get; private set; } = new() { Period = "30" };
    public SummaryCards Summary90 { get; private set; } = new() { Period = "90" };
    public SummaryCards SummaryAll { get; private set; } = new() { Period = "all" };
    public List<CorrelationResult> Correlations { get; private set; } = new();
    public TopLists Tops { get; private set; } = new();
    public Distribution Distribution { get; private set; } = new();
    public DateTime UpdatedAt { get; private set; }

    public void Update(SummaryCards s30, SummaryCards s90, SummaryCards sAll, List<CorrelationResult> corr, TopLists tops, Distribution dist)
    { lock (_lock) { Summary30 = s30; Summary90 = s90; SummaryAll = sAll; Correlations = corr; Tops = tops; Distribution = dist; UpdatedAt = DateTime.UtcNow; } }

    public SummaryCards GetSummary(string period) => period switch { "30" => Summary30, "90" => Summary90, _ => SummaryAll };
}