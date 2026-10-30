namespace Privacy.Storage;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;
using Privacy.InternalContracts;
public static class PrivacyMapper
{
    public static IPinCredentialAnemicModel MapToModel(PinCredentialEntity e)
    { var m = new AnemicModel { Id = e.Id, Version = e.Version }; m.Root = PinCredentialRoot.CreateInstance(e.Salt, e.Hash, e.Iterations); m.Locked = e.Locked; return m; }
    public static void MapToEntity(IPinCredentialAnemicModel m, PinCredentialEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.SourceId = "pin"; e.Salt = m.Root.Salt; e.Hash = m.Root.Hash; e.Iterations = m.Root.Iterations; e.Locked = m.Locked; }
    public static PinCredentialReadModel MapToReadModel(PinCredentialEntity e) => new PinCredentialReadModel { Id = e.Id, SourceId = e.SourceId, Salt = e.Salt, Hash = e.Hash, Iterations = e.Iterations, Locked = e.Locked };
}