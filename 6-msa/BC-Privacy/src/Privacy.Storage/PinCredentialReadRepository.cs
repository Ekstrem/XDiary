namespace Privacy.Storage;
using Microsoft.EntityFrameworkCore;
using Privacy.InternalContracts;
public sealed class PinCredentialReadRepository : IPinCredentialQueryRepository
{
    private readonly ReadDbContext _db;
    public PinCredentialReadRepository(ReadDbContext db) => _db = db;
    public async Task<PinCredentialReadModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default)
    { var e = await _db.Credentials.FirstOrDefaultAsync(x => x.SourceId == sourceId, ct); return e is null ? null : PrivacyMapper.MapToReadModel(e); }
}