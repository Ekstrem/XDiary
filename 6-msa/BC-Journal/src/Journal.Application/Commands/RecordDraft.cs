namespace Journal.Application.Commands;

/// <summary>
/// Черновик записи (RecordDraft) — полный набор полей визарда.
/// </summary>
public class RecordDraft
{
    public List<string> Partners { get; set; } = new();
    public string StartedAt { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public string Place { get; set; } = string.Empty;
    public List<string> Positions { get; set; } = new();
    public List<string> Costumes { get; set; } = new();
    public List<string> Toys { get; set; } = new();
    public List<string> Fantasies { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> Contraceptives { get; set; } = new();
    public int ContraceptiveCount { get; set; }
    public List<Journal.Domain.OrgasmsEntry> Orgasms { get; set; } = new();
    public bool Garmin { get; set; }
    public string GarminId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}
