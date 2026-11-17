namespace Catalog.Domain.Implementation;

using Catalog.Domain.Abstraction;

// record (positional) — getter-only VO, как RecordRoot в Journal.Domain.
internal sealed record CatalogItemRoot(string Kind, string Name) : ICatalogItemRoot
{
    public static ICatalogItemRoot CreateInstance(string kind, string name)
        => new CatalogItemRoot(kind, name);
}
