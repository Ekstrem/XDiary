namespace GarminGateway.Storage;
using Microsoft.EntityFrameworkCore;
using GarminGateway.InternalContracts;
public sealed class GarminGatewayReadRepository : IGarminGatewayQueryRepository
{
    private readonly ReadDbContext _db;
    public GarminGatewayReadRepository(ReadDbContext db) => _db = db;
    public async Task<GarminSyncReadModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default)
    { var e = await _db.Sync.FirstOrDefaultAsync(x => x.SourceId == sourceId, ct); return e is null ? null : GarminGatewayMapper.MapToReadModel(e); }
    public async Task<GarminSyncReadModel?> GetByClientIdSourceIdAsync(Guid clientId, string sourceId, CancellationToken ct = default)
    { var e = await _db.Sync.FirstOrDefaultAsync(x => x.ClientId == clientId && x.SourceId == sourceId, ct); return e is null ? null : GarminGatewayMapper.MapToReadModel(e); }
}