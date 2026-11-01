namespace Client.InternalContracts;
public interface IClientQueryRepository
{
    Task<ClientReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientReadModel>> ListAsync(CancellationToken cancellationToken = default);
    Task<ClientReadModel?> GetByPhoneAsync(string phone, CancellationToken cancellationToken = default);
}