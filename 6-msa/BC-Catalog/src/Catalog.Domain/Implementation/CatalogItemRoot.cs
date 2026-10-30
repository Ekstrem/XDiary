namespace Catalog.Domain.Implementation;
using Catalog.Domain.Abstraction;
public sealed class CatalogItemRoot : ICatalogItemRoot
{
    public string Kind { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    private CatalogItemRoot() { }
    public static ICatalogItemRoot CreateInstance(string kind, string name) => new CatalogItemRoot { Kind = kind, Name = name };
}