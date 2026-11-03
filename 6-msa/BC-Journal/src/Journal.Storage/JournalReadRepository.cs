namespace Journal.Storage;
using Journal.InternalContracts;
using Microsoft.EntityFrameworkCore;
public sealed class JournalReadRepository : IJournalQueryRepository
{
    private readonly ReadDbContext _db;
    public JournalReadRepository(ReadDbContext db) => _db = db;
    public async Task<JournalReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.Records.FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null || e.Deleted ? null : JournalMapper.MapToReadModel(e);
    }
    public async Task<IReadOnlyList<JournalReadModel>> ListAsync(Guid clientId, int? limit, int? offset, CancellationToken ct = default)
    {
        IQueryable<RecordEntity> q = _db.Records.AsNoTracking().Where(x => !x.Deleted && x.ClientId == clientId).OrderByDescending(x => x.StartedAt);
        if (offset is > 0) q = q.Skip(offset.Value);
        if (limit is > 0) q = q.Take(limit.Value);
        return (await q.ToListAsync(ct)).Select(JournalMapper.MapToReadModel).ToList();
    }
    public async Task<IReadOnlyList<JournalReadModel>> ListAllAsync(int? limit, int? offset, CancellationToken ct = default)
    {
        IQueryable<RecordEntity> q = _db.Records.AsNoTracking().Where(x => !x.Deleted).OrderByDescending(x => x.StartedAt);
        if (offset is > 0) q = q.Skip(offset.Value);
        if (limit is > 0) q = q.Take(limit.Value);
        return (await q.ToListAsync(ct)).Select(JournalMapper.MapToReadModel).ToList();
    }
    public async Task<int> CountAsync(CancellationToken ct = default) => await _db.Records.CountAsync(ct);
    public async Task<RecentDefaultsView?> GetRecentAsync(CancellationToken ct = default)
    {
        var e = await _db.Records.AsNoTracking().Where(x => !x.Deleted).OrderByDescending(x => x.StartedAt).FirstOrDefaultAsync(ct);
        if (e is null) return null;
        var partners = JournalMapper.ToList(e.PartnersJson);
        return new RecentDefaultsView
        {
            LastPartner = partners.Count > 0 ? new PartnerRef { Id = partners[0], Name = partners[0] } : null,
            LastPlace = string.IsNullOrWhiteSpace(e.Place) ? null : e.Place,
            LastPositions = JournalMapper.ToList(e.PositionsJson),
            LastTags = JournalMapper.ToList(e.TagsJson),
            LastContraceptives = JournalMapper.ToList(e.ContraceptivesJson),
            LastFantasies = JournalMapper.ToList(e.FantasiesJson)
        };
    }
    public async Task<IReadOnlyList<string>> GetPlacesAsync(CancellationToken ct = default)
        => (await _db.Records.AsNoTracking().Where(x => !x.Deleted && x.Place != string.Empty).Select(x => x.Place).Distinct().OrderBy(x => x).ToListAsync(ct)).ToList();
    public async Task<IReadOnlyList<string>> GetFantasiesAsync(CancellationToken ct = default)
    {
        var rows = await _db.Records.AsNoTracking().Where(x => !x.Deleted).Select(x => x.FantasiesJson).ToListAsync(ct);
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var json in rows) foreach (var f in JournalMapper.ToList(json)) set.Add(f);
        return set.ToList();
    }
}