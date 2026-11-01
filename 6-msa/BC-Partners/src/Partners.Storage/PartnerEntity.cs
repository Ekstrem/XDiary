namespace Partners.Storage;
public sealed class PartnerEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public long Version { get; set; }
}