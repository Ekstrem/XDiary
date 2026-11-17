namespace GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Abstraction;
public sealed record SyncConfig(IReadOnlyList<string> Metrics, int WindowDays) : ISyncConfig
{
    public static ISyncConfig CreateInstance(IEnumerable<string> metrics, int windowDays) => new SyncConfig(metrics.ToList(), windowDays);
}