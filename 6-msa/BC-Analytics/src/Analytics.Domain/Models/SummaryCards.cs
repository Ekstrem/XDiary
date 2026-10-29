namespace Analytics.Domain.Models;

/// <summary>
/// Сводные карточки за период (SummaryCards).
/// </summary>
public class SummaryCards
{
    public string Period { get; set; } = "30";
    public int Sessions { get; set; }
    public int TotalMinutes { get; set; }
    public int AvgDuration { get; set; }
    public int Orgasms { get; set; }
    public double AvgRating { get; set; }
}
