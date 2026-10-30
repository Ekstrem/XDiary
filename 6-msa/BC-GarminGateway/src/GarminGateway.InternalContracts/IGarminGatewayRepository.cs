namespace GarminGateway.InternalContracts;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using XDiary.Contracts;
public interface IGarminGatewayRepository : IRepository<IGarminGateway, IGarminGatewayAnemicModel>
{
    Task<IGarminGatewayAnemicModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default);
}