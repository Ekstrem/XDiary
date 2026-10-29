using DigiTFactory.Libraries.SeedWorks.Definition;

namespace Catalog.Domain;

/// <summary>
/// Ограниченный контекст «Партнёры».
/// Единственный агрегат контекста — CatalogItem (профиль партнёра).
/// </summary>
public interface ICatalog : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста партнёров.
/// </summary>
public class CatalogBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "Catalog";
    public int MicroserviceVersion => 1;
}
