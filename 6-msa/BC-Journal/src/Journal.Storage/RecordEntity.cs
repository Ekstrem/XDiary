namespace Journal.Storage;
public sealed class RecordEntity
{
    public Guid Id { get; set; }
    public string PartnersJson { get; set; } = "[]";
    public string StartedAt { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public string Place { get; set; } = string.Empty;
    public string PositionsJson { get; set; } = "[]";
    public string TagsJson { get; set; } = "[]";
    public string CostumesJson { get; set; } = "[]";
    public string ToysJson { get; set; } = "[]";
    public string FantasiesJson { get; set; } = "[]";
    public string ContraceptivesJson { get; set; } = "[]";
    public int ContraceptiveCount { get; set; }
    public string OrgasmsJson { get; set; } = "[]";
    public bool Garmin { get; set; }
    public string GarminId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public long Version { get; set; }
}