namespace GarminGateway.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.InternalContracts;
using XDiary.Contracts;
public sealed class GarminGatewayCommandRepository : IGarminGatewayRepository
{
    private readonly CommandDbContext _db;
    public GarminGatewayCommandRepository(CommandDbContext db) => _db = db;
    public async Task<IGarminGatewayAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Sync.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : GarminGatewayMapper.MapToModel(e); }
    public async Task<IGarminGatewayAnemicModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default)
    { var e = await _db.Sync.FirstOrDefaultAsync(x => x.SourceId == sourceId, ct); return e is null ? null : GarminGatewayMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = await _db.Sync.FirstOrDefaultAsync(x => x.SourceId == m.Root.SourceId, ct);
        if (e is null) { e = new GarminSyncEntity { Id = m.Id }; _db.Sync.Add(e); }
        GarminGatewayMapper.MapToEntity(m, e);
        await _db.SaveChangesAsync(ct);
    }
}