namespace Catalog.Storage;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;
using Catalog.InternalContracts;
public static class CatalogMapper
{
    public static ICatalogAnemicModel MapToModel(CatalogItemEntity e)
    {
        var m = new AnemicModel { Id = e.Id, Version = e.Version };
        m.Root = CatalogItemRoot.CreateInstance(e.Kind, e.Name);
        m.Photo = e.Photo; m.Notes = e.Notes; m.Brand = e.Brand; m.Archived = e.Archived; m.FantasyPartner = e.FantasyPartner;
        return m;
    }
    public static void MapToEntity(ICatalogAnemicModel m, CatalogItemEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.Kind = m.Root.Kind; e.Name = m.Root.Name; e.Photo = m.Photo; e.Notes = m.Notes; e.Brand = m.Brand; e.Archived = m.Archived; e.FantasyPartner = m.FantasyPartner; }
    public static CatalogReadModel MapToReadModel(CatalogItemEntity e) => new CatalogReadModel { Id = e.Id, Kind = e.Kind, Name = e.Name, Photo = e.Photo, Notes = e.Notes, Brand = e.Brand, Archived = e.Archived, FantasyPartner = e.FantasyPartner };
}