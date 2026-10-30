namespace Portability.Storage;
using Microsoft.EntityFrameworkCore;
using Portability.InternalContracts;
public sealed class PortabilityReadRepository : IPortabilityQueryRepository
{
    private readonly ReadDbContext _db;
    public PortabilityReadRepository(ReadDbContext db) => _db = db;
    public async Task<CsvDocumentReadModel?> GetLastAsync(CancellationToken ct = default)
    { var e = await _db.Documents.AsNoTracking().OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct); return e is null ? null : PortabilityMapper.MapToReadModel(e); }
}