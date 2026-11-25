namespace Partners.Api.Events;
using System.Text.Json;
using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using Partners.Domain;
using Partners.Storage;
using XDiary.Contracts;

/// <summary>
/// BE-059 (E5): РґРѕСЃС‚Р°РІРєР° outbox В«С…РѕС‚СЏ Р±С‹ РѕРґРёРЅ СЂР°Р·В». Р РµРєРѕРЅСЃС‚СЂСѓРёСЂСѓРµС‚ DomainEvent РёР· Payload
/// ($type-РґРёСЃРєСЂРёРјРёРЅР°С‚РѕСЂ IValueObject) Рё РїРѕРІС‚РѕСЂРЅРѕ РїСѓР±Р»РёРєСѓРµС‚; ProcessedAt РїРѕРјРµС‡Р°РµС‚СЃСЏ РїРѕСЃР»Рµ СѓСЃРїРµС…Р°.
/// </summary>
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher> _log;
    public OutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcher> log)
    { _scopeFactory = scopeFactory; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
                var bus = scope.ServiceProvider.GetRequiredService<IEventBusProducer>();
                var serializer = ValueObjectSerializerFactory.CreateDefault();

                var pending = await db.Outbox
                    .Where(x => x.ProcessedAt == null)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(stoppingToken);

                foreach (var msg in pending)
                {
                    var vos = serializer.DeserializeWithLegacy(msg.Payload);
                    // BE-059 (@AR 08:14): re-delivery РѕР±СЏР·Р°РЅР° РЅРµСЃС‚Рё id РђР“Р Р•Р“РђРўРђ, Р° РЅРµ PK СЃС‚СЂРѕРєРё outbox.
                    var aggregateId = msg.AggregateId ?? msg.Id;
                    var domainEvent = new DomainEvent<IPartners>(
                        aggregateId,
                        0,
                        null,
                        vos!.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        new BoundedContextDescription<IPartners>("Partners", 0),
                        DomainOperationResultEnum.Success,
                        null);
                    await bus.PublishAsync(domainEvent);
                    msg.ProcessedAt = DateTime.UtcNow;
                    msg.Attempts++;
                }

                if (pending.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    _log.LogInformation("[outbox] delivered={N}", pending.Count);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[outbox] РѕС€РёР±РєР° РґРёСЃРїРµС‚С‡РµСЂР°");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
