namespace Journal.Storage;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using XDiary.Contracts;
public sealed class JournalCommandRepository : IRepository<IJournal, IJournalAnemicModel>
{
    private readonly IAnemicModelRepository<IJournal, IJournalAnemicModel> _repo;
    private readonly IEventBusProducer _bus;
    public JournalCommandRepository(IAnemicModelRepository<IJournal, IJournalAnemicModel> repo, IEventBusProducer bus) { _repo = repo; _bus = bus; }
    // BE-056 (E1): агрегат читается из DF-репозитория (event-sourced), а не из EF-state CommandDbContext.Records.
    public async Task<IJournalAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var list = await _repo.GetById(id, ct); return list.LastOrDefault(); }
    public async Task SaveAsync(AggregateResult<IJournal, IJournalAnemicModel> result, CancellationToken ct = default)
    {
        // E1: command-path пишет событием, а не SaveChangesAsync — store заполняет consumer.
        await _bus.PublishAsync(result.Event);
    }
}