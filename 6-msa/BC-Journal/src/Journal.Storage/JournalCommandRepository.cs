namespace Journal.Storage;
using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
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
    private readonly IEventStoreRepository<IJournal, IJournalAnemicModel> _store;
    private readonly IEventBusProducer _bus;
    public JournalCommandRepository(IJournalQueryRepository readRepo, RecordEventReplay replay, IEventStoreRepository<IJournal, IJournalAnemicModel> store, IEventBusProducer bus) { _readRepo = readRepo; _replay = replay; _store = store; _bus = bus; }
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
        // Канон §6: BusAdapter «сохранить И опубликовать» — событие персистится в event store ДО публикации,
        // а не пишется consumer'ом fire-and-forget (при падении consumer событие не терялось молча).
        var jsonOptions = new JsonSerializerOptions();
        jsonOptions.Converters.Add(new IValueObjectJsonConverter());
        var cmdName = result.Event.ChangedValueObjects is not null
            && result.Event.ChangedValueObjects.TryGetValue("CommandName", out var cn)
            && cn is StringValueObject cns
            ? cns.Value
            : (result.Event.Command?.GetType().Name ?? string.Empty);
        var entry = new DomainEventEntry
        {
            Id = result.Event.Id,
            Version = result.Event.Version,
            CorrelationToken = result.Event.CorrelationToken,
            BoundedContext = result.Event.ContextName,
            CommandName = cmdName,
            SubjectName = result.Event.ContextName,
            ChangedValueObjectsJson = JsonSerializer.Serialize(result.Event.ChangedValueObjects, jsonOptions),
            Result = result.Event.Result.ToString(),
            CreatedAt = DateTime.UtcNow,
        };
        await _store.SaveEventAsync(entry, ct);
        await _bus.PublishAsync(result.Event);
    }
}