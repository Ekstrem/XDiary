using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;

namespace Journal.InternalContracts;

/// <summary>
/// Материализованное представление записи (проекция в Read Store, store `records`).
/// </summary>
public class JournalReadModel : IReadModel<IJournal>
{
    public Guid Id { get; set; }
    public string PartnerId { get; set; } = string.Empty;
    public string StartedAt { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public string Place { get; set; } = string.Empty;
    public List<string> Positions { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> Costumes { get; set; } = new();
    public List<string> Toys { get; set; } = new();
    public List<string> Fantasies { get; set; } = new();
    public List<string> Contraceptives { get; set; } = new();
    public int ContraceptiveCount { get; set; }
    public int OrgasmsMe { get; set; }
    public int OrgasmsPartner { get; set; }
    public List<string> OrgasmMeType { get; set; } = new();
    public List<string> OrgasmPartnerType { get; set; } = new();
    public int OrgasmMeRating { get; set; }
    public int OrgasmPartnerRating { get; set; }
    public bool Garmin { get; set; }
    public string GarminId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;

    public long Version { get; set; }
    public DateTimeOffset RecordCreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RecordUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
