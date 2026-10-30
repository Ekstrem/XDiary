namespace AppleHealthGateway.InternalContracts;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using XDiary.Contracts;
public interface IAppleHealthGatewayRepository : IRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>
{
    Task<IAppleHealthGatewayAnemicModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default);
}