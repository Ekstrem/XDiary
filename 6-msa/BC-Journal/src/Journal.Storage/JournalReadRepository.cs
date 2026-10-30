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
        return e is null ? null : JournalMapper.MapToReadModel(e);
    }
    public async Task<IReadOnlyList<JournalReadModel>> ListAsync(CancellationToken ct = default)
        => (await _db.Records.AsNoTracking().OrderByDescending(x => x.StartedAt).ToListAsync(ct)).Select(JournalMapper.MapToReadModel).ToList();
    public async Task<int> CountAsync(CancellationToken ct = default) => await _db.Records.CountAsync(ct);
}