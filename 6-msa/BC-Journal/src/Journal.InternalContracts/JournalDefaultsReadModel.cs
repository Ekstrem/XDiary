namespace Journal.InternalContracts;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// Pre-computed «дефолты» клиента (E1 §1.2): вместо Distinct/OrderBy в query
/// проекция поддерживает готовые списки мест/фантазий и последний акт.
/// Ключ — ClientId (scope по клиенту).
/// </summary>
public sealed class JournalDefaultsReadModel : IReadModel<Journal.Domain.IJournal>
{
    public Guid Id { get; set; } // = ClientId (ключ per-client)
    public Guid ClientId { get; set; }
    public string LastPartnerId { get; set; } = string.Empty;
    public string LastPlace { get; set; } = string.Empty;
    public IReadOnlyList<string> Places { get; set; } = new List<string>();
    public IReadOnlyList<string> Fantasies { get; set; } = new List<string>();
    public IReadOnlyList<string> Positions { get; set; } = new List<string>();
    public IReadOnlyList<string> Tags { get; set; } = new List<string>();
    public IReadOnlyList<string> Contraceptives { get; set; } = new List<string>();
}
