namespace AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.Domain.Abstraction;
public sealed record ImportConfig(IReadOnlyList<string> Metrics) : IImportConfig
{
    public static IImportConfig CreateInstance(IEnumerable<string> metrics) => new ImportConfig(metrics.ToList());
}
