namespace Privacy.InternalContracts;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using XDiary.Contracts;
public interface IPinCredentialRepository : IRepository<IPrivacy, IPinCredentialAnemicModel>
{
    Task<IPinCredentialAnemicModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default);
    Task<IPinCredentialAnemicModel?> GetByClientIdSourceIdAsync(Guid clientId, string sourceId, CancellationToken ct = default);
}