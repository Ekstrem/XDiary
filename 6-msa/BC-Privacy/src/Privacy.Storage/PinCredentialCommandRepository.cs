namespace Privacy.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.InternalContracts;
using XDiary.Contracts;
public sealed class PinCredentialCommandRepository : IPinCredentialRepository
{
    private readonly CommandDbContext _db;
    public PinCredentialCommandRepository(CommandDbContext db) => _db = db;
    public async Task<IPinCredentialAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Credentials.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : PrivacyMapper.MapToModel(e); }
    public async Task<IPinCredentialAnemicModel?> GetBySourceIdAsync(string sourceId, CancellationToken ct = default)
    { var e = await _db.Credentials.FirstOrDefaultAsync(x => x.SourceId == sourceId, ct); return e is null ? null : PrivacyMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<IPrivacy, IPinCredentialAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = await _db.Credentials.FirstOrDefaultAsync(x => x.SourceId == "pin", ct);
        if (e is null) { e = new PinCredentialEntity { Id = m.Id }; _db.Credentials.Add(e); }
        PrivacyMapper.MapToEntity(m, e);
        await _db.SaveChangesAsync(ct);
    }
}