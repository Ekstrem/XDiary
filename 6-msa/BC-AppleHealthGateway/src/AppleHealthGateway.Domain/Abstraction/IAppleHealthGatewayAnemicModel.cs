namespace AppleHealthGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IAppleHealthGatewayAnemicModel : IAnemicModel<IAppleHealthGateway>
{
    IAppleImportRoot Root { get; }
    IImportConfig ImportConfig { get; }
    string LastImport { get; }
    IReadOnlyList<string> ProcessedFiles { get; }
}