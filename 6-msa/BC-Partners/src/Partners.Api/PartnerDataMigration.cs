namespace Partners.Api;

using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Microsoft.EntityFrameworkCore;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.Storage;

/// <summary>
/// BE-061 (G1): одноразовая миграция существующих EF-строк `partners`
/// в события. Идемпотентна по id (GetEventCountAsync).
/// </summary>
public sealed class PartnerDataMigration
{
    private readonly CommandDbContext _db;
    private readonly IEventBusProducer _bus;
    private readonly IEventStoreRepository<IPartners, IPartnerAnemicModel> _store;

    public PartnerDataMigration(
        CommandDbContext db,
        IEventBusProducer bus,
        IEventStoreRepository<IPartners, IPartnerAnemicModel> store)
    {
        _db = db;
        _bus = bus;
        _store = store;
    }

    public async Task<int> MigrateAsync(CancellationToken ct = default)
    {
        var partners = await _db.Partners.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
        var migrated = 0;

        foreach (var e in partners)
        {
            var existing = await _store.GetEventCountAsync(e.Id, ct);
            if (existing > 0) continue;

            var model = PartnerMapper.MapToModel(e);
            // событие «создание» без бизнес-валидации: Rename на то же имя даёт полный срез.
            var result = Aggregate.CreateInstance(model).Rename(model.Root.Name);

            if (result.Result == DomainOperationResultEnum.Success)
            {
                await _bus.PublishAsync(result.Event);
                migrated++;
            }
        }

        return migrated;
    }
}
