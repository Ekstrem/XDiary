namespace GarminGateway.InternalContracts;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using XDiary.Contracts;
public interface IGarminGatewayRepository : IRepository<IGarminGateway, IGarminGatewayAnemicModel>
{
    Task<IGarminGatewayAnemicModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default);
    Task<IGarminGatewayAnemicModel?> GetByClientIdSourceIdAsync(Guid clientId, string sourceId, CancellationToken ct = default);
}