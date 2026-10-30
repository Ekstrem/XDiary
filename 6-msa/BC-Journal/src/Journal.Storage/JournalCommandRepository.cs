namespace Journal.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Microsoft.EntityFrameworkCore;
using XDiary.Contracts;
public sealed class JournalCommandRepository : IRepository<IJournal, IJournalAnemicModel>
{
    private readonly CommandDbContext _db;
    public JournalCommandRepository(CommandDbContext db) => _db = db;
    public async Task<IJournalAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.Records.FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? null : JournalMapper.MapToModel(e);
    }
    public async Task SaveAsync(AggregateResult<IJournal, IJournalAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = await _db.Records.FirstOrDefaultAsync(x => x.Id == m.Id, ct);
        if (e is null) { e = new RecordEntity { Id = m.Id }; _db.Records.Add(e); }
        JournalMapper.MapToEntity(m, e);
        await _db.SaveChangesAsync(ct);
    }
}