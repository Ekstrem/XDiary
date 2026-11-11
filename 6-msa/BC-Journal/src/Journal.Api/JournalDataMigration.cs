namespace Journal.Storage;

using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// BE-061 (G1): одноразовая миграция существующих EF-строк `journal.records`
/// в события (по одному событию на запись). Идемпотентна по id записи
/// (проверка GetEventCountAsync). Запускается отдельным шагом, не в проде-авто.
/// </summary>
public sealed class JournalDataMigration
{
    private readonly CommandDbContext _db;
    private readonly IEventBusProducer _bus;
    private readonly IEventStoreRepository<IJournal, IJournalAnemicModel> _store;

    public JournalDataMigration(
        CommandDbContext db,
        IEventBusProducer bus,
        IEventStoreRepository<IJournal, IJournalAnemicModel> store)
    {
        _db = db;
        _bus = bus;
        _store = store;
    }

    public async Task<(int Migrated, int SkippedNoOwner)> MigrateAsync(CancellationToken ct = default)
    {
        var records = await _db.Records.AsNoTracking().OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var migrated = 0;
        var skippedNoOwner = 0;

        foreach (var e in records)
        {
            // G1 (@AR 19:00): запись без владельца не мигрируем молча — считаем отдельно.
            if (e.ClientId is null) { skippedNoOwner++; continue; }

            // идемпотентность: если события по id уже есть — пропускаем.
            var existing = await _store.GetEventCountAsync(e.Id, ct);
            if (existing > 0) continue;

            var model = JournalMapper.MapToModel(e);
            // Событие «завершение» (полный срез) — в агрегате нет «создания с полным срезом»; зафиксировано как отклонение в BE-061.
            var aggregate = Aggregate.CreateInstance(model);
            var result = aggregate.Complete();

            if (result.Result == DomainOperationResultEnum.Success)
            {
                await _bus.PublishAsync(result.Event);
                migrated++;
            }
        }

        return (migrated, skippedNoOwner);
    }
}
