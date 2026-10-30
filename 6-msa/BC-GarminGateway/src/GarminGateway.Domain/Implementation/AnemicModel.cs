namespace GarminGateway.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain.Abstraction;
public sealed class AnemicModel : IGarminGatewayAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();
    public IGarminSyncRoot Root { get; set; } = null!;
    public ICredentials Credentials { get; set; } = Implementation.Credentials.CreateInstance("", "", "");
    public ISyncConfig SyncConfig { get; set; } = Implementation.SyncConfig.CreateInstance(new List<string>(), 0);
    public string LastSync { get; set; } = string.Empty;
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root, ["Credentials"] = Credentials, ["SyncConfig"] = SyncConfig };
}