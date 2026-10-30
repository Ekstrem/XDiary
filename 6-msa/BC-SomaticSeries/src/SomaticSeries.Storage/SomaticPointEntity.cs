namespace SomaticSeries.Storage;
public sealed class SomaticPointEntity
{
    public Guid Id { get; set; }
    public string T { get; set; } = string.Empty;
    public int Hr { get; set; }
    public int BodyBattery { get; set; }
    public long Version { get; set; }
}