namespace DailyMetrics.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.InternalContracts;
using XDiary.Contracts;
public sealed class DailyMetricsCommandRepository : IDailyMetricsRepository
{
    private readonly CommandDbContext _db;
    public DailyMetricsCommandRepository(CommandDbContext db) => _db = db;
    public async Task<IDailyMetricsAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Daily.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : DailyMetricsMapper.MapToModel(e); }
    public async Task<IDailyMetricsAnemicModel?> GetByDateAsync(string date, CancellationToken ct = default)
    { var e = await _db.Daily.FirstOrDefaultAsync(x => x.Date == date, ct); return e is null ? null : DailyMetricsMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = await _db.Daily.FirstOrDefaultAsync(x => x.Date == m.Root.Date, ct);
        if (e is null) { e = new DailyMetricsEntity { Id = m.Id }; _db.Daily.Add(e); }
        DailyMetricsMapper.MapToEntity(m, e);
        await _db.SaveChangesAsync(ct);
    }
}