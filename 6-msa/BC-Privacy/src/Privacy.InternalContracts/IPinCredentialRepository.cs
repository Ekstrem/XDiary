namespace Privacy.InternalContracts;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using XDiary.Contracts;
public interface IPinCredentialRepository : IRepository<IPrivacy, IPinCredentialAnemicModel>
{
    Task<IPinCredentialAnemicModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default);
}