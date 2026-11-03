namespace Client.Storage;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.InternalContracts;
public static class ClientMapper
{
    public static IClientAnemicModel MapToModel(ClientEntity e)
    {
        var m = new AnemicModel { Id = e.Id, Version = e.Version };
        m.Root = ClientRoot.CreateInstance(e.Phone);
        m.OtpCode = e.OtpCode; m.OtpExpiresAt = e.OtpExpiresAt; m.OtpAttempts = e.OtpAttempts; m.Verified = e.Verified; m.LinkedPartnerId = e.LinkedPartnerId;
        return m;
    }
    public static void MapToEntity(IClientAnemicModel m, ClientEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.Phone = m.Root.Phone; e.OtpCode = m.OtpCode; e.OtpExpiresAt = m.OtpExpiresAt; e.OtpAttempts = m.OtpAttempts; e.Verified = m.Verified; e.LinkedPartnerId = m.LinkedPartnerId; }
    public static ClientReadModel MapToReadModel(ClientEntity e) => new ClientReadModel { Id = e.Id, Phone = e.Phone, Verified = e.Verified, LinkedPartnerId = e.LinkedPartnerId };
}