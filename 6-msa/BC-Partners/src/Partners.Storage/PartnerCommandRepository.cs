namespace Partners.Storage;
using System.Text.Json;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.InternalContracts;
using XDiary.Contracts;
public sealed class PartnerCommandRepository : IRepository<IPartners, IPartnerAnemicModel>
{
    private readonly CommandDbContext _db;
    private readonly IPartnersQueryRepository _readRepo;
    private readonly IAnemicModelRepository<IPartners, IPartnerAnemicModel> _events;
    private readonly IEventBusProducer _bus;
    public PartnerCommandRepository(CommandDbContext db, IPartnersQueryRepository readRepo, IAnemicModelRepository<IPartners, IPartnerAnemicModel> events, IEventBusProducer bus) { _db = db; _readRepo = readRepo; _events = events; _bus = bus; }
    // BE-088: быстрый путь — read-модель; при промахе (свежая запись, лаг проекции) — пересборка из событий.
    public async Task<IPartnerAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var read = await _readRepo.GetByIdUnscopedAsync(id, ct);
        if (read is not null)
            return new AnemicModel { Id = read.Id, Root = PartnerRoot.CreateInstance(read.Name), ClientId = read.ClientId, DeletedAt = read.DeletedAt, Photo = read.Photo, Gender = read.Gender, Notes = read.Notes };
        var events = await _events.GetById(id, ct);
        return events.LastOrDefault();
    }
    public async Task SaveAsync(AggregateResult<IPartners, IPartnerAnemicModel> result, CancellationToken ct = default)
    {
        // BE-073 диагностика (@TL 15:02): null-событие = причина «200 без схемы».
        Console.WriteLine($"[Save] event={(result.Event is null ? "<null>" : result.Event.Id.ToString())}");
        // BE-059: persist события в outbox (backstop «хотя бы один раз») + текущая доставка.
        if (result.Event is not null)
        {
            var jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new IValueObjectJsonConverter());
            _db.Outbox.Add(new OutboxMessage
            {
                // @TL 00:17 — Id строки outbox обязан быть уникальным на событие: result.Event.Id — это
                // id АГРЕГАТА (IHasKey), он один на все события агрегата → PK-конфликт на второй команде (500).
                EventType = "Partners",
                // $type-дискриминатор для реконструкции IValueObject (BE-059).
                Payload = JsonSerializer.Serialize(result.Event.ChangedValueObjects, jsonOptions),
                CreatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
        }
        // E1: command-path пишет событием (Partners по тому же конвейеру, что Journal).
        await _bus.PublishAsync(result.Event);
    }
}