namespace Partners.Api.Events;
using Microsoft.EntityFrameworkCore;
using Partners.Storage;

/// <summary>
/// BE-059 (E5): фоновый диспетчер outbox. Пока — диагностика отставания
/// (сколько событий ожидают доставки); доставка (реконструкция DomainEvent из Payload)
/// — следующий шаг, требующий полиморфной десериализации IValueObject.
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
                var pending = await db.Outbox.CountAsync(x => x.ProcessedAt == null, stoppingToken);
                if (pending > 0) _log.LogInformation("[outbox] pending={N}", pending);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[outbox] ошибка диспетчера");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
