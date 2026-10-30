namespace Privacy.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain.Abstraction;
public sealed class AnemicModel : IPinCredentialAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();
    public IPinCredentialRoot Root { get; set; } = PinCredentialRoot.CreateInstance("", "", 0);
    public bool Locked { get; set; }
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root };
}