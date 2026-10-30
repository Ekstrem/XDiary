namespace Catalog.Storage;
public sealed class CatalogItemEntity
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public string FantasyPartner { get; set; } = string.Empty;
    public long Version { get; set; }
}