namespace Portability.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.InternalContracts;
using XDiary.Contracts;
public sealed class PortabilityCommandRepository : IPortabilityRepository
{
    private readonly CommandDbContext _db;
    public PortabilityCommandRepository(CommandDbContext db) => _db = db;
    public async Task<ICsvDocumentAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : PortabilityMapper.MapToModel(e); }
    public async Task<ICsvDocumentAnemicModel?> GetLastAsync(CancellationToken ct = default)
    { var e = await _db.Documents.AsNoTracking().OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct); return e is null ? null : PortabilityMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<IPortability, ICsvDocumentAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = new CsvDocumentEntity { Id = m.Id };
        PortabilityMapper.MapToEntity(m, e);
        _db.Documents.Add(e);
        await _db.SaveChangesAsync(ct);
    }
}