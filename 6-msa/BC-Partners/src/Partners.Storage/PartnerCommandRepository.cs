namespace Partners.Storage;
using System.Text.Json;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Storage.Outbox;
using XDiary.Contracts;
public sealed class PartnerCommandRepository : IRepository<IPartners, IPartnerAnemicModel>
{
    private readonly CommandDbContext _db;
    private readonly IAnemicModelRepository<IPartners, IPartnerAnemicModel> _repo;
    private readonly IEventBusProducer _bus;
    public PartnerCommandRepository(CommandDbContext db, IAnemicModelRepository<IPartners, IPartnerAnemicModel> repo, IEventBusProducer bus) { _db = db; _repo = repo; _bus = bus; }
    // BE-056 (E1): агрегат читается из DF-репозитория (event-sourced), а не из EF-state CommandDbContext.Partners.
    public async Task<IPartnerAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { var list = await _repo.GetById(id, ct); return list.LastOrDefault(); }
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