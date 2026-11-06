namespace Catalog.Storage;
using Microsoft.EntityFrameworkCore;
using Catalog.InternalContracts;
public sealed class CatalogReadRepository : ICatalogQueryRepository
{
    private readonly ReadDbContext _db;
    public CatalogReadRepository(ReadDbContext db) => _db = db;
    public async Task<CatalogReadModel?> GetByIdAsync(Guid id, Guid clientId, CancellationToken ct = default)
    { var e = await _db.Items.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct); return e is null ? null : CatalogMapper.MapToReadModel(e); }
    public async Task<IReadOnlyList<CatalogReadModel>> ListAsync(Guid clientId, CancellationToken ct = default)
        => (await _db.Items.AsNoTracking().Where(x => x.ClientId == clientId).OrderBy(x => x.Kind).ThenBy(x => x.Name).ToListAsync(ct)).Select(CatalogMapper.MapToReadModel).ToList();
    public async Task<int> CountAsync(CancellationToken ct = default) => await _db.Items.CountAsync(ct);
}