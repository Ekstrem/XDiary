namespace Catalog.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using XDiary.Contracts;
public sealed class CatalogCommandRepository : IRepository<ICatalog, ICatalogAnemicModel>
{
    private readonly CommandDbContext _db;
    public CatalogCommandRepository(CommandDbContext db) => _db = db;
    public async Task<ICatalogAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Items.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : CatalogMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<ICatalog, ICatalogAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = await _db.Items.FirstOrDefaultAsync(x => x.Id == m.Id, ct);
        if (e is null) { e = new CatalogItemEntity { Id = m.Id }; _db.Items.Add(e); }
        CatalogMapper.MapToEntity(m, e);
        await _db.SaveChangesAsync(ct);
    }
}