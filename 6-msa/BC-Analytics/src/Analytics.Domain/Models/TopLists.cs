namespace Analytics.Domain.Models;
public sealed class TopLists
{
    public List<CountEntry> Positions { get; set; } = new();
    public List<CountEntry> Places { get; set; } = new();
    public List<CountEntry> Partners { get; set; } = new();
    public List<CountEntry> Fantasies { get; set; } = new();
}
public sealed class CountEntry
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}
public sealed class Distribution
{
    public List<CountEntry> Weekdays { get; set; } = new();
    public List<CountEntry> Hours { get; set; } = new();
}