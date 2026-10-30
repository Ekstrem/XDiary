namespace AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.Domain.Abstraction;
public sealed class ImportConfig : IImportConfig
{
    public IReadOnlyList<string> Metrics { get; private set; } = new List<string>();
    private ImportConfig() { }
    public static IImportConfig CreateInstance(IEnumerable<string> metrics) => new ImportConfig { Metrics = metrics.ToList() };
}