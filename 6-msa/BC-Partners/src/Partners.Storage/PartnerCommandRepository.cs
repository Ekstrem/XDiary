namespace Partners.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using Partners.Domain;
using Partners.Domain.Abstraction;
using XDiary.Contracts;
public sealed class PartnerCommandRepository : IRepository<IPartners, IPartnerAnemicModel>
{
    private readonly CommandDbContext _db;
    public PartnerCommandRepository(CommandDbContext db) => _db = db;
    public async Task<IPartnerAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Partners.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : PartnerMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<IPartners, IPartnerAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = await _db.Partners.FirstOrDefaultAsync(x => x.Id == m.Id, ct);
        if (e is null) { e = new PartnerEntity { Id = m.Id }; _db.Partners.Add(e); }
        PartnerMapper.MapToEntity(m, e);
        await _db.SaveChangesAsync(ct);
    }
}