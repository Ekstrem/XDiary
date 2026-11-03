namespace Journal.InternalContracts;

public interface IJournalQueryRepository
{
    Task<JournalReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalReadModel>> ListAsync(Guid clientId, int? limit, int? offset, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<RecentDefaultsView?> GetRecentAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetPlacesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetFantasiesAsync(CancellationToken cancellationToken = default);
}
