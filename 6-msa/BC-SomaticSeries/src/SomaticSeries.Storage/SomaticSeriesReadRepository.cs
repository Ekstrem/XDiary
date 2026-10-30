namespace SomaticSeries.Storage;
using Microsoft.EntityFrameworkCore;
using SomaticSeries.InternalContracts;
public sealed class SomaticSeriesReadRepository : ISomaticSeriesQueryRepository
{
    private readonly ReadDbContext _db;
    public SomaticSeriesReadRepository(ReadDbContext db) => _db = db;
    public async Task<SomaticSeriesReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Points.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : SomaticSeriesMapper.MapToReadModel(e); }
    public async Task<IReadOnlyList<SomaticSeriesReadModel>> GetSliceAsync(string fromT, string toT, CancellationToken ct = default)
        => (await _db.Points.AsNoTracking().Where(x => x.T.CompareTo(fromT) >= 0 && x.T.CompareTo(toT) <= 0).OrderBy(x => x.T).ToListAsync(ct)).Select(SomaticSeriesMapper.MapToReadModel).ToList();
    public async Task<int> CountAsync(CancellationToken ct = default) => await _db.Points.CountAsync(ct);
}