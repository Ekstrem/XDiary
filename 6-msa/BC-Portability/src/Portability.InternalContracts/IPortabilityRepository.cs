namespace Portability.InternalContracts;
using Portability.Domain;
using Portability.Domain.Abstraction;
using XDiary.Contracts;
public interface IPortabilityRepository : IRepository<IPortability, ICsvDocumentAnemicModel>
{
    Task<ICsvDocumentAnemicModel?> GetLastAsync(CancellationToken ct = default);
}