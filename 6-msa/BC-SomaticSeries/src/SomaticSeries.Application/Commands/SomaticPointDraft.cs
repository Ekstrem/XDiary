namespace SomaticSeries.Application.Commands;

/// <summary>
/// Черновик минутной точки соматики (SomaticPoint).
/// </summary>
public class SomaticPointDraft
{
    /// <summary>Минутная метка «YYYY-MM-DDTHH:mm».</summary>
    public string T { get; set; } = string.Empty;
    public int Hr { get; set; }
    public int BodyBattery { get; set; }
}
