namespace AppleHealthGateway.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.InternalContracts;
using XDiary.Contracts;
public sealed class AppleHealthGatewayCommandRepository : IAppleHealthGatewayRepository
{
    private readonly CommandDbContext _db;
    public AppleHealthGatewayCommandRepository(CommandDbContext db) => _db = db;
    public async Task<IAppleHealthGatewayAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Import.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : AppleHealthGatewayMapper.MapToModel(e); }
    public async Task<IAppleHealthGatewayAnemicModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default)
    { var e = await _db.Import.FirstOrDefaultAsync(x => x.SourceId == sourceId, ct); return e is null ? null : AppleHealthGatewayMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = await _db.Import.FirstOrDefaultAsync(x => x.SourceId == m.Root.SourceId, ct);
        if (e is null) { e = new AppleImportEntity { Id = m.Id }; _db.Import.Add(e); }
        AppleHealthGatewayMapper.MapToEntity(m, e);
        await _db.SaveChangesAsync(ct);
    }
}