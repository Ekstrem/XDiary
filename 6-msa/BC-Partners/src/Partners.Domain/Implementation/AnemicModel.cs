namespace Partners.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain.Abstraction;
public sealed class AnemicModel : IPartnerAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();
    public IPartnerRoot Root { get; set; } = null!;
    public string Photo { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root };
}