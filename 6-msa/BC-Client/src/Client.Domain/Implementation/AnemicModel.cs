namespace Client.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Client.Domain.Abstraction;
public sealed class AnemicModel : IClientAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();
    public IClientRoot Root { get; set; } = null!;
    public string OtpCode { get; set; } = string.Empty;
    public DateTime? OtpExpiresAt { get; set; }
    public int OtpAttempts { get; set; }
    public bool Verified { get; set; }
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root };
}