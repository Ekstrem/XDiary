namespace Analytics.Api.Precompute;
public sealed class JournalRecord
{
    public string StartedAt { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public List<OrgasmDto> Orgasms { get; set; } = new();
    public List<string> Positions { get; set; } = new();
    public List<string> Fantasies { get; set; } = new();
    public string Place { get; set; } = string.Empty;
    public List<string> Partners { get; set; } = new();
}
public sealed class OrgasmDto
{
    public string Participant { get; set; } = "me";
    public int Count { get; set; }
    public int Rating { get; set; }
}