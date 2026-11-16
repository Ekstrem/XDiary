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
    private readonly RecordEventReplay _replay;
    private readonly IEventBusProducer _bus;
    public JournalCommandRepository(IJournalQueryRepository readRepo, RecordEventReplay replay, IEventBusProducer bus) { _readRepo = readRepo; _replay = replay; _bus = bus; }
    // BE-088: быстрый путь — read-модель; при промахе (свежая запись, лаг проекции) — реальный replay из событий.
    public async Task<IJournalAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var read = await _readRepo.GetByIdAsync(id, ct);
        if (read is not null)
        {
            var m = new AnemicModel
            {
                Id = read.Id,
                ClientId = read.ClientId,
                Root = RecordRoot.CreateInstance(read.Partners, string.IsNullOrWhiteSpace(read.StartedAt) ? default : DateTimeOffset.Parse(read.StartedAt)),
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
                State = (Journal.Domain.RecordState)read.State,
                Orgasms = read.Orgasms.Select(o => Orgasms.CreateInstance(o.Participant, o.Count, o.Types, o.Rating)).ToList(),
            };
            return m;
        }
        return await _replay.ReplayAsync(id, ct);
    }
    public async Task SaveAsync(AggregateResult<IJournal, IJournalAnemicModel> result, CancellationToken ct = default)
    {
        // E1: command-path пишет событием, а не SaveChangesAsync — store заполняет consumer.
        await _bus.PublishAsync(result.Event);
    }
}