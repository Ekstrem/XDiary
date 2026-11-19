namespace Catalog.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain.Abstraction;

/// <summary>QG-102: начальное (пустое) состояние агрегата — фабрика с явным Id.</summary>
internal static class DefaultAnemicModel
{
    public static ICatalogAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
