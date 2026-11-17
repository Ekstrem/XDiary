namespace Client.Storage;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.InternalContracts;
public static class ClientMapper
{
    public static IClientAnemicModel MapToModel(ClientEntity e)
    {
        return new AnemicModel { Id = e.Id, Version = e.Version, Root = ClientRoot.CreateInstance(e.Phone), OtpCode = e.OtpCode, OtpExpiresAt = e.OtpExpiresAt, OtpAttempts = e.OtpAttempts, DisplayName = e.DisplayName, Gender = e.Gender, PhotoUrl = e.PhotoUrl, Verified = e.Verified };
    }
    public static void MapToEntity(IClientAnemicModel m, ClientEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.Phone = m.Root.Phone; e.OtpCode = m.OtpCode; e.OtpExpiresAt = m.OtpExpiresAt; e.OtpAttempts = m.OtpAttempts; e.DisplayName = m.DisplayName; e.Gender = m.Gender; e.PhotoUrl = m.PhotoUrl; e.Verified = m.Verified; }
    public static ClientReadModel MapToReadModel(ClientEntity e) => new ClientReadModel { Id = e.Id, Phone = e.Phone, DisplayName = e.DisplayName, Gender = e.Gender, PhotoUrl = e.PhotoUrl, Verified = e.Verified };
}