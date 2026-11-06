namespace Journal.Storage;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Journal.InternalContracts;
using XDiary.Contracts;
public sealed class JournalCommandRepository : IRepository<IJournal, IJournalAnemicModel>
{
    private readonly IJournalQueryRepository _readRepo;
    private readonly IEventBusProducer _bus;
    public JournalCommandRepository(IJournalQueryRepository readRepo, IEventBusProducer bus) { _readRepo = readRepo; _bus = bus; }
    // @DO 00:30 — командная таблица (StateOnly) пуста по дизайну E1; агрегат для UPDATE читается из read-модели.
    public async Task<IJournalAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var read = await _readRepo.GetByIdAsync(id, ct);
        if (read is null) return null;
        var m = new AnemicModel
        {
            Id = read.Id,
            ClientId = read.ClientId,
            DurationMin = read.DurationMin,
            Place = read.Place,
            Positions = read.Positions,
            Costumes = read.Costumes,
            Toys = read.Toys,
            Fantasies = read.Fantasies,
            Tags = read.Tags,
            Contraceptives = read.Contraceptives,
            ContraceptiveCount = read.ContraceptiveCount,
            Garmin = read.Garmin,
            GarminId = read.GarminId,
            CreatedAt = read.CreatedAt,
            UpdatedAt = read.UpdatedAt,
            Deleted = read.Deleted,
            Orgasms = read.Orgasms.Select(o => Orgasms.CreateInstance(o.Participant, o.Count, o.Types, o.Rating)).ToList(),
        };
        m.Root = RecordRoot.CreateInstance(read.Partners, read.StartedAt);
        return m;
    }
    public async Task SaveAsync(AggregateResult<IJournal, IJournalAnemicModel> result, CancellationToken ct = default)
    {
        // E1: command-path пишет событием, а не SaveChangesAsync — store заполняет consumer.
        await _bus.PublishAsync(result.Event);
    }
}