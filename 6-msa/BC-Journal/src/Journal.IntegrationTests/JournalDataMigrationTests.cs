using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Events;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// BE-061 (G1): тест миграции EF-строк → события. Инвариант «до/после»:
/// по одному событию на запись с владельцем, идемпотентность по множеству id
/// (первый прогон → N, повторный → 0), записи без владельца — skipped_no_owner.
/// </summary>
public class JournalDataMigrationTests
{
    private sealed class FakeBus : IEventBusProducer
    {
        public int Published;
        public void Publish<TBoundedContext>(IDomainEvent<TBoundedContext> e) where TBoundedContext : IBoundedContext { Published++; }
        public Task PublishAsync<TBoundedContext>(IDomainEvent<TBoundedContext> e) where TBoundedContext : IBoundedContext { Published++; return Task.CompletedTask; }
    }

    private sealed class FakeStore : IEventStoreRepository<IJournal, IJournalAnemicModel>
    {
        // Множество id с событиями (а не одна константа) — идемпотентность проверяется данными.
        public HashSet<Guid> EventIds = new();
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(EventIds.Contains(id) ? 1 : 0);
        public Task<List<IJournalAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IJournalAnemicModel>());
        public Task<IJournalAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IJournalAnemicModel>(null!);
        public Task<IJournalAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IJournalAnemicModel>(null!);
    }

    private static (CommandDbContext db, List<Guid> withOwner, List<Guid> noOwner) MakeDb()
    {
        var options = new DbContextOptionsBuilder<CommandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CommandDbContext(options);
        var withOwner = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var noOwner = new List<Guid> { Guid.NewGuid() };
        db.Records.AddRange(
            withOwner.Select(id => new RecordEntity { Id = id, ClientId = Guid.NewGuid(), StartedAt = "2026-10-29T21:00", DurationMin = 30, Place = "Отель", PartnersJson = "[\"partner-1\"]" }));
        db.Records.AddRange(
            noOwner.Select(id => new RecordEntity { Id = id, ClientId = null, StartedAt = "2026-10-30T21:00", DurationMin = 45, Place = "Дом", PartnersJson = "[\"partner-2\"]" }));
        db.SaveChanges();
        return (db, withOwner, noOwner);
    }

    [Fact]
    public async Task Migrate_PublishesOneEventPerOwnerRecord_AndIsIdempotent()
    {
        var (db, withOwner, noOwner) = MakeDb();
        var bus = new FakeBus();
        var store = new FakeStore();
        var migration = new JournalDataMigration(db, bus, store);

        // Первый прогон: по событию на запись с владельцем; без владельца — skipped.
        var (migrated, skipped) = await migration.MigrateAsync();
        Assert.Equal(withOwner.Count, migrated);
        Assert.Equal(noOwner.Count, skipped);        // записи без владельца не мигрированы молча
        Assert.Equal(withOwner.Count, bus.Published);

        // После первого прогона события записаны (моделируем запись в стор).
        foreach (var id in withOwner) store.EventIds.Add(id);

        // Второй прогон: 0 новых (идемпотентность по множеству id).
        var (migrated2, _) = await migration.MigrateAsync();
        Assert.Equal(0, migrated2);
        Assert.Equal(withOwner.Count, bus.Published);

        // Третий прогон: тоже 0.
        var (migrated3, _) = await migration.MigrateAsync();
        Assert.Equal(0, migrated3);
        Assert.Equal(withOwner.Count, bus.Published);
    }
}
