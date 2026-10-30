namespace Portability.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain.Abstraction;
public sealed class AnemicModel : ICsvDocumentAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();
    public ICsvDocumentRoot Root { get; set; } = null!;
    public IReadOnlyList<string> Rows { get; set; } = new List<string>();
    public string Kind { get; set; } = string.Empty;
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root };
}