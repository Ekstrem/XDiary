namespace Client.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using Client.Domain;
using Client.Domain.Abstraction;
using XDiary.Contracts;
public sealed class ClientCommandRepository : IRepository<IClients, IClientAnemicModel>
{
    private readonly CommandDbContext _db;
    public ClientCommandRepository(CommandDbContext db) => _db = db;
    public async Task<IClientAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Clients.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : ClientMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<IClients, IClientAnemicModel> result, CancellationToken ct = default)
    {
        var m = result.Model();
        var e = await _db.Clients.FirstOrDefaultAsync(x => x.Id == m.Id, ct);
        if (e is null) { e = new ClientEntity { Id = m.Id }; _db.Clients.Add(e); }
        ClientMapper.MapToEntity(m, e);
        await _db.SaveChangesAsync(ct);
    }
}