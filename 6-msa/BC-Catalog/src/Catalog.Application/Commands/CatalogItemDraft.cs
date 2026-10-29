namespace Catalog.Application.Commands;

/// <summary>
/// Черновик элемента каталога (ItemDraft из Command API).
/// </summary>
public class CatalogItemDraft
{
    /// <summary>costume | toy | fantasy | contraception</summary>
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public string Partner { get; set; } = string.Empty;
}
