namespace Client.Storage;
using Microsoft.EntityFrameworkCore;
using Client.InternalContracts;
public sealed class ClientReadRepository : IClientQueryRepository
{
    private readonly ReadDbContext _db;
    public ClientReadRepository(ReadDbContext db) => _db = db;
    public async Task<ClientReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : ClientMapper.MapToReadModel(e); }
    public async Task<IReadOnlyList<ClientReadModel>> ListAsync(CancellationToken ct = default)
        => (await _db.Clients.AsNoTracking().OrderBy(x => x.Phone).ToListAsync(ct)).Select(ClientMapper.MapToReadModel).ToList();
    public async Task<ClientReadModel?> GetByPhoneAsync(string phone, CancellationToken ct = default)
    { var e = await _db.Clients.FirstOrDefaultAsync(x => x.Phone == phone, ct); return e is null ? null : ClientMapper.MapToReadModel(e); }
}