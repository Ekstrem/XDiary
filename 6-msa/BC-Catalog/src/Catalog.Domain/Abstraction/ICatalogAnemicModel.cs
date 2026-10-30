namespace Catalog.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ICatalogAnemicModel : IAnemicModel<ICatalog>
{
    ICatalogItemRoot Root { get; }
    string Photo { get; }
    string Notes { get; }
    string Brand { get; }
    bool Archived { get; }
    string FantasyPartner { get; }
}