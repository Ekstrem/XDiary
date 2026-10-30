namespace Catalog.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain.Abstraction;
public sealed class AnemicModel : ICatalogAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();
    public ICatalogItemRoot Root { get; set; } = null!;
    public string Photo { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public string FantasyPartner { get; set; } = string.Empty;
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root };
}