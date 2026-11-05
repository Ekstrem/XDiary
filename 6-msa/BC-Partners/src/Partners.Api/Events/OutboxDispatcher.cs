namespace Partners.Api.Events;
using System.Text.Json;
using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Microsoft.EntityFrameworkCore;
using Partners.Domain;
using Partners.Storage;
using Partners.Storage.Outbox;

/// <summary>
/// BE-059 (E5): доставка outbox «хотя бы один раз». Реконструирует DomainEvent из Payload
/// ($type-дискриминатор IValueObject) и повторно публикует; ProcessedAt помечается после успеха.
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
                var db = scope.ServiceProvider.GetRequiredService<CommandDbContext>();
                var bus = scope.ServiceProvider.GetRequiredService<IEventBusProducer>();
                var jsonOptions = new JsonSerializerOptions();
                jsonOptions.Converters.Add(new IValueObjectJsonConverter());

                var pending = await db.Outbox
                    .Where(x => x.ProcessedAt == null)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(stoppingToken);

                foreach (var msg in pending)
                {
                    var vos = JsonSerializer.Deserialize<IDictionary<string, IValueObject>>(msg.Payload, jsonOptions);
                    var domainEvent = new DomainEvent<IPartners>(
                        msg.Id,
                        0,
                        null,
                        vos!,
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
                _log.LogError(ex, "[outbox] ошибка диспетчера");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
