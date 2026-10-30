namespace SomaticSeries.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using XDiary.Contracts;
public sealed class SomaticSeriesCommandRepository : IRepository<ISomaticSeries, ISomaticSeriesAnemicModel>
{
    private readonly CommandDbContext _db;
    public SomaticSeriesCommandRepository(CommandDbContext db) => _db = db;
    public async Task<ISomaticSeriesAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Points.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : SomaticSeriesMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = await _db.Points.FirstOrDefaultAsync(x => x.Id == m.Id, ct);
        if (e is null) { e = new SomaticPointEntity { Id = m.Id }; _db.Points.Add(e); }
        SomaticSeriesMapper.MapToEntity(m, e);
        await _db.SaveChangesAsync(ct);
    }
}