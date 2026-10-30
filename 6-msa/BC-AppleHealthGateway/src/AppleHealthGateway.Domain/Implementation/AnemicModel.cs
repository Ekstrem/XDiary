namespace AppleHealthGateway.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain.Abstraction;
public sealed class AnemicModel : IAppleHealthGatewayAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();
    public IAppleImportRoot Root { get; set; } = null!;
    public IImportConfig ImportConfig { get; set; } = Implementation.ImportConfig.CreateInstance(new List<string>());
    public string LastImport { get; set; } = string.Empty;
    public IReadOnlyList<string> ProcessedFiles { get; set; } = new List<string>();
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root, ["ImportConfig"] = ImportConfig };
}