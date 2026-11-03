namespace Journal.InternalContracts;

public interface IJournalQueryRepository
{
    Task<JournalReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalReadModel>> ListAsync(Guid clientId, int? limit, int? offset, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalReadModel>> ListAllAsync(int? limit, int? offset, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<RecentDefaultsView?> GetRecentAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetPlacesAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetFantasiesAsync(Guid clientId, CancellationToken cancellationToken = default);
}
