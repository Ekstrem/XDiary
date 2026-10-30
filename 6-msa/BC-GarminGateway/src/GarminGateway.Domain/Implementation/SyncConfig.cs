namespace GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Abstraction;
public sealed class SyncConfig : ISyncConfig
{
    public IReadOnlyList<string> Metrics { get; private set; } = new List<string>();
    public int WindowDays { get; private set; }
    private SyncConfig() { }
    public static ISyncConfig CreateInstance(IEnumerable<string> metrics, int windowDays) => new SyncConfig { Metrics = metrics.ToList(), WindowDays = windowDays };
}