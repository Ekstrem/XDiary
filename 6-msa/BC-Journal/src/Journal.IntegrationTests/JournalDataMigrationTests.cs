using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Events;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// BE-061 (G1): тест миграции EF-строк → события. Инвариант «до/после»:
/// по одному событию на запись, идемпотентность (повторный прогон — 0 новых).
/// </summary>
public class JournalDataMigrationTests
{
    private sealed class FakeBus : IEventBusProducer
    {
        public int Published;
        public Task PublishAsync(IDomainEvent<IJournal> e, CancellationToken ct = default) { Published++; return Task.CompletedTask; }
    }

    private sealed class FakeStore : IEventStoreRepository<IJournal, IJournalAnemicModel>
    {
        public int EventCount;
        public Task SaveEventAsync(DomainEventEntry entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveSnapshotAsync(Guid id, long version, string aggregateJson, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetEventCountAsync(Guid id, CancellationToken ct = default) => Task.FromResult(EventCount);
        public Task<List<IJournalAnemicModel>> GetById(Guid id, CancellationToken ct = default) => Task.FromResult(new List<IJournalAnemicModel>());
        public Task<IJournalAnemicModel> GetByIdAndVersion(Guid id, long version, CancellationToken ct = default) => Task.FromResult<IJournalAnemicModel>(null!);
        public Task<IJournalAnemicModel> GetByCorrelationToken(Guid correlationToken, CancellationToken ct = default) => Task.FromResult<IJournalAnemicModel>(null!);
    }

    private static (CommandDbContext db, int n) MakeDb()
    {
        var options = new DbContextOptionsBuilder<CommandDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CommandDbContext(options);
        var records = new List<RecordEntity>
        {
            new RecordEntity { Id = Guid.NewGuid(), ClientId = null, StartedAt = "2026-10-29T21:00", DurationMin = 30, Place = "Отель", PartnersJson = "[\"partner-1\"]" },
            new RecordEntity { Id = Guid.NewGuid(), ClientId = Guid.NewGuid(), StartedAt = "2026-10-30T21:00", DurationMin = 45, Place = "Дом", PartnersJson = "[\"partner-2\"]" },
        };
        db.Records.AddRange(records);
        db.SaveChanges();
        return (db, records.Count);
    }

    [Fact]
    public async Task Migrate_PublishesOneEventPerRecord_AndIsIdempotent()
    {
        var (db, n) = MakeDb();
        var bus = new FakeBus();
        var store = new FakeStore();
        var migration = new JournalDataMigration(db, bus, store);

        // Когда: первый прогон.
        var first = await migration.MigrateAsync();
        Assert.Equal(n, first);              // по событию на каждую запись
        Assert.Equal(n, bus.Published);

        // Тогда: идемпотентность — события уже есть, повторный прогон не создаёт дублей.
        store.EventCount = 1;
        var second = await migration.MigrateAsync();
        Assert.Equal(0, second);
        Assert.Equal(n, bus.Published);      // событий не прибавилось
    }
}
