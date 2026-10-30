namespace AppleHealthGateway.Storage;
using Microsoft.EntityFrameworkCore;
using AppleHealthGateway.InternalContracts;
public sealed class AppleHealthGatewayReadRepository : IAppleHealthGatewayQueryRepository
{
    private readonly ReadDbContext _db;
    public AppleHealthGatewayReadRepository(ReadDbContext db) => _db = db;
    public async Task<AppleImportReadModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default)
    { var e = await _db.Import.FirstOrDefaultAsync(x => x.SourceId == sourceId, ct); return e is null ? null : AppleHealthGatewayMapper.MapToReadModel(e); }
}