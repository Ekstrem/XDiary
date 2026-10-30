namespace Journal.Domain;

/// <summary>
/// Оргазм участника акта: participant («me» | partnerId), count, types[], rating (0–5).
/// </summary>
public class OrgasmsEntry
{
    public string Participant { get; set; } = "me";
    public int Count { get; set; }
    public List<string> Types { get; set; } = new();
    public int Rating { get; set; }
}
