namespace Privacy.Storage;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;
using Privacy.InternalContracts;
public static class PrivacyMapper
{
    public static IPinCredentialAnemicModel MapToModel(PinCredentialEntity e)
    { return new AnemicModel { Id = e.Id, Version = e.Version, Root = PinCredentialRoot.CreateInstance(e.ClientId, e.Salt, e.Hash, e.Iterations), Locked = e.Locked }; }
    public static void MapToEntity(IPinCredentialAnemicModel m, PinCredentialEntity e)
    { e.Version = m.Version; e.ClientId = m.Root.ClientId; e.SourceId = "pin"; e.Salt = m.Root.Salt; e.Hash = m.Root.Hash; e.Iterations = m.Root.Iterations; e.Locked = m.Locked; }
    public static PinCredentialReadModel MapToReadModel(PinCredentialEntity e) => new PinCredentialReadModel { Id = e.Id, ClientId = e.ClientId, SourceId = e.SourceId, Salt = e.Salt, Hash = e.Hash, Iterations = e.Iterations, Locked = e.Locked };
}
