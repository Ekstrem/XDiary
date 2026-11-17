namespace Partners.Storage;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.InternalContracts;
public static class PartnerMapper
{
    public static IPartnerAnemicModel MapToModel(PartnerEntity e)
    {
        return new AnemicModel
        {
            Id = e.Id,
            Version = e.Version,
            Root = PartnerRoot.CreateInstance(e.Name),
            Photo = e.Photo,
            Gender = e.Gender,
            Notes = e.Notes,
            ClientId = e.ClientId,
            DeletedAt = e.DeletedAt,
        };
    }
    public static void MapToEntity(IPartnerAnemicModel m, PartnerEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.Name = m.Root.Name; e.Photo = m.Photo; e.Gender = m.Gender; e.Notes = m.Notes; e.ClientId = m.ClientId; e.DeletedAt = m.DeletedAt; }
    public static PartnersReadModel MapToReadModel(PartnerEntity e) => new PartnersReadModel { Id = e.Id, Name = e.Name, Photo = e.Photo, Gender = e.Gender, Notes = e.Notes, ClientId = e.ClientId, DeletedAt = e.DeletedAt };
}