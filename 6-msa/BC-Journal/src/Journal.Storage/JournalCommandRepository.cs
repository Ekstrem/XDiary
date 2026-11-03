namespace Journal.Storage;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Microsoft.EntityFrameworkCore;
using XDiary.Contracts;
public sealed class JournalCommandRepository : IRepository<IJournal, IJournalAnemicModel>
{
    private readonly CommandDbContext _db;
    private readonly IEventBusProducer _bus;
    public JournalCommandRepository(CommandDbContext db, IEventBusProducer bus) { _db = db; _bus = bus; }
    public async Task<IJournalAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.Records.FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? null : JournalMapper.MapToModel(e);
    }
    public async Task SaveAsync(AggregateResult<IJournal, IJournalAnemicModel> result, CancellationToken ct = default)
    {
        // E1: command-path пишет событием, а не SaveChangesAsync — store заполняет consumer.
        await _bus.PublishAsync(result.Event);
    }
}