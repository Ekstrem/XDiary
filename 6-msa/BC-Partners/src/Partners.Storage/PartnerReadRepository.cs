namespace Partners.Storage;
using Microsoft.EntityFrameworkCore;
using Partners.InternalContracts;
public sealed class PartnerReadRepository : IPartnersQueryRepository
{
    private readonly ReadDbContext _db;
    public PartnerReadRepository(ReadDbContext db) => _db = db;
    public async Task<PartnersReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Partners.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : PartnerMapper.MapToReadModel(e); }
    public async Task<IReadOnlyList<PartnersReadModel>> ListAsync(CancellationToken ct = default)
        => (await _db.Partners.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)).Select(PartnerMapper.MapToReadModel).ToList();
    public async Task<int> CountAsync(CancellationToken ct = default) => await _db.Partners.CountAsync(ct);
}