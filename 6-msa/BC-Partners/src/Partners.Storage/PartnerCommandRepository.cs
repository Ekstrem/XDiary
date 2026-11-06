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
    private readonly IEventBusProducer _bus;
    public PartnerCommandRepository(CommandDbContext db, IPartnersQueryRepository readRepo, IEventBusProducer bus) { _db = db; _readRepo = readRepo; _bus = bus; }
    // @DO 00:30 — командная таблица (EF/StateOnly) пуста по дизайну E1; агрегат для UPDATE читается из read-модели.
    public async Task<IPartnerAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var read = await _readRepo.GetByIdAsync(id, ct);
        if (read is null) return null;
        return new AnemicModel { Id = read.Id, Root = PartnerRoot.CreateInstance(read.Name), ClientId = read.ClientId, DeletedAt = read.DeletedAt, Photo = read.Photo, Gender = read.Gender, Notes = read.Notes };
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
                Id = result.Event.Id,
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