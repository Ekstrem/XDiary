namespace Journal.Application.Commands;

/// <summary>
/// Черновик записи — входные данные визарда (RecordDraft из Command API).
/// </summary>
public class RecordDraft
{
    public string StartedAt { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public string Partner { get; set; } = string.Empty;
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
}
