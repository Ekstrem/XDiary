namespace Partners.Storage;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using Partners.Domain;
using Partners.Domain.Abstraction;
using XDiary.Contracts;
public sealed class PartnerCommandRepository : IRepository<IPartners, IPartnerAnemicModel>
{
    private readonly CommandDbContext _db;
    private readonly IEventBusProducer _bus;
    public PartnerCommandRepository(CommandDbContext db, IEventBusProducer bus) { _db = db; _bus = bus; }
    public async Task<IPartnerAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var e = await _db.Partners.FirstOrDefaultAsync(x => x.Id == id, ct); return e is null ? null : PartnerMapper.MapToModel(e); }
    public async Task SaveAsync(AggregateResult<IPartners, IPartnerAnemicModel> result, CancellationToken ct = default)
    {
        // E1: command-path пишет событием (Partners по тому же конвейеру, что Journal).
        await _bus.PublishAsync(result.Event);
    }
}