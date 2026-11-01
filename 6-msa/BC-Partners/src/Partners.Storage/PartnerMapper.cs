namespace Partners.Storage;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.InternalContracts;
public static class PartnerMapper
{
    public static IPartnerAnemicModel MapToModel(PartnerEntity e)
    {
        var m = new AnemicModel { Id = e.Id, Version = e.Version };
        m.Root = PartnerRoot.CreateInstance(e.Name);
        m.Photo = e.Photo; m.Gender = e.Gender; m.Notes = e.Notes; m.Self = e.Self;
        return m;
    }
    public static void MapToEntity(IPartnerAnemicModel m, PartnerEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.Name = m.Root.Name; e.Photo = m.Photo; e.Gender = m.Gender; e.Notes = m.Notes; e.Self = m.Self; }
    public static PartnersReadModel MapToReadModel(PartnerEntity e) => new PartnersReadModel { Id = e.Id, Name = e.Name, Photo = e.Photo, Gender = e.Gender, Notes = e.Notes, Self = e.Self };
}