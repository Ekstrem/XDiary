namespace AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.Domain.Abstraction;
internal sealed record ImportConfig(IReadOnlyList<string> Metrics) : IImportConfig
{
    public static IImportConfig CreateInstance(IEnumerable<string> metrics) => new ImportConfig(metrics.ToList());
}
