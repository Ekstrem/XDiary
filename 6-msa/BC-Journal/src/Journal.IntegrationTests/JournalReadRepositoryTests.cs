using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.InternalContracts;
using Journal.Storage;
using Xunit;

/// <summary>Проверка фильтрации read-модели (ListAsync по ClientId, GetByIdAsync) поверх DF IReadRepository.</summary>
public class JournalReadRepositoryTests
{
    private sealed class FakeRead : IReadRepository<IJournal, JournalReadModel>
    {
        private readonly List<JournalReadModel> _items;
        public FakeRead(params JournalReadModel[] items) => _items = items.ToList();
        public Task<JournalReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<JournalReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JournalReadModel>>(_items);
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult((long)_items.Count);
    }

    private sealed class FakeDir : IReadRepository<IJournal, PartnersDirectoryReadModel>
    {
        public Task<PartnersDirectoryReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<PartnersDirectoryReadModel?>(null);
        public Task<IReadOnlyList<PartnersDirectoryReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartnersDirectoryReadModel>>(Array.Empty<PartnersDirectoryReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class FakeDefaults : IReadRepository<IJournal, JournalDefaultsReadModel>
    {
        public Task<JournalDefaultsReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<JournalDefaultsReadModel?>(null);
        public Task<IReadOnlyList<JournalDefaultsReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JournalDefaultsReadModel>>(Array.Empty<JournalDefaultsReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }

    private static JournalReadRepository CreateRepo(params JournalReadModel[] records)
        => new(new FakeRead(records), new FakeDir(), new FakeDefaults(), null!);

    [Fact]
    public async Task ListAsync_ReturnsOnlyMine_NotOthers_NorNull()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var repo = CreateRepo(
            new JournalReadModel { Id = Guid.NewGuid(), ClientId = a, StartedAt = "2026-11-09T10:00:00" },
            new JournalReadModel { Id = Guid.NewGuid(), ClientId = b, StartedAt = "2026-11-09T11:00:00" },
            new JournalReadModel { Id = Guid.NewGuid(), ClientId = null, StartedAt = "2026-11-09T12:00:00" });
        var list = await repo.ListAsync(a, null, null);
        Assert.Single(list);
        Assert.Equal("2026-11-09T10:00:00", list[0].StartedAt);
    }

    [Fact]
    public async Task ListAsync_PartnerParticipant_DoesNotSeeRecord()
    {
        var owner = Guid.NewGuid(); var partner = Guid.NewGuid();
        var repo = CreateRepo(
            new JournalReadModel { Id = Guid.NewGuid(), ClientId = owner, StartedAt = "2026-11-09T10:00:00" });
        var list = await repo.ListAsync(partner, null, null);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetByIdAsync_ExposesClientId()
    {
        var owner = Guid.NewGuid(); var id = Guid.NewGuid();
        var repo = CreateRepo(new JournalReadModel { Id = id, ClientId = owner, StartedAt = "2026-11-09T10:00:00" });
        var rec = await repo.GetByIdAsync(id);
        Assert.NotNull(rec);
        Assert.Equal(owner, rec.ClientId);
    }
}
