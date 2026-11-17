namespace AppleHealthGateway.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain.Abstraction;
internal sealed record AnemicModel : IAppleHealthGatewayAnemicModel
{
    public static IAppleHealthGatewayAnemicModel Create(Guid id) => new AnemicModel { Id = id };
    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();
    public IAppleImportRoot Root { get; init; } = null!;
    public IImportConfig ImportConfig { get; init; } = Implementation.ImportConfig.CreateInstance(new List<string>());
    public string LastImport { get; init; } = string.Empty;
    public IReadOnlyList<string> ProcessedFiles { get; init; } = new List<string>();
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject>
    {
        ["Root"] = Root,
        ["ImportConfig"] = ImportConfig,
        ["LastImport"] = new StringValueObject(LastImport),
        ["ProcessedFiles"] = new StringListValueObject(ProcessedFiles),
    };
}