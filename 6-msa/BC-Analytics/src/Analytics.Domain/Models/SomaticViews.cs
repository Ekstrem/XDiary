namespace Analytics.Domain.Models;
public sealed class Thresholds
{
    public int MaxHr { get; set; }
    public int MinBodyBattery { get; set; }
}
public sealed class TrendsView
{
    public List<CountEntry> Stress { get; set; } = new();
    public List<CountEntry> Sleep { get; set; } = new();
}
public sealed class PhysioProfile
{
    public double AvgHr { get; set; }
    public double AvgBodyBattery { get; set; }
    public int Samples { get; set; }
}