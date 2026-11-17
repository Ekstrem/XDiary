namespace Portability.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain.Abstraction;

// Иммутабельный агрегат — поля init-only, не set (мутация через `with`, не через сеттеры).
public sealed record AnemicModel : ICsvDocumentAnemicModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();
    public ICsvDocumentRoot Root { get; init; } = null!;
    public IReadOnlyList<string> Rows { get; init; } = new List<string>();
    public string Kind { get; init; } = string.Empty;
    public Guid? ClientId { get; init; }
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root };
}
