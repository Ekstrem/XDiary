namespace GarminGateway.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain.Abstraction;
internal sealed record AnemicModel : IGarminGatewayAnemicModel
{
    public static IGarminGatewayAnemicModel Create(Guid id) => new AnemicModel { Id = id };
    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();
    public IGarminSyncRoot Root { get; init; } = null!;
    public ICredentials Credentials { get; init; } = Implementation.Credentials.CreateInstance("", "", "");
    public ISyncConfig SyncConfig { get; init; } = Implementation.SyncConfig.CreateInstance(new List<string>(), 0);
    public string LastSync { get; init; } = string.Empty;
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root, ["Credentials"] = Credentials, ["SyncConfig"] = SyncConfig };
}