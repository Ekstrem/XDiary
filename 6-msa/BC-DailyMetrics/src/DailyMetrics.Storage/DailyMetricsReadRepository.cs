namespace DailyMetrics.Storage;
using Microsoft.EntityFrameworkCore;
using DailyMetrics.InternalContracts;
public sealed class DailyMetricsReadRepository : IDailyMetricsQueryRepository
{
    private readonly ReadDbContext _db;
    public DailyMetricsReadRepository(ReadDbContext db) => _db = db;
    public async Task<IReadOnlyList<DailyMetricsReadModel>> GetSeriesAsync(string fromDate, string toDate, CancellationToken ct = default)
        => (await _db.Daily.AsNoTracking().Where(x => x.Date.CompareTo(fromDate) >= 0 && x.Date.CompareTo(toDate) <= 0).OrderBy(x => x.Date).ToListAsync(ct)).Select(DailyMetricsMapper.MapToReadModel).ToList();
    public async Task<int> CountAsync(CancellationToken ct = default) => await _db.Daily.CountAsync(ct);
}