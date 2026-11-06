using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Partners.Domain;
using Partners.InternalContracts;
using Partners.Storage;
using Xunit;

/// <summary>Проверка фильтрации read-модели партнёров (ListAsync по ClientId) поверх DF IReadRepository.</summary>
public class PartnerReadRepositoryTests
{
    private sealed class FakeRead : IReadRepository<IPartners, PartnersReadModel>
    {
        private readonly List<PartnersReadModel> _items;
        public FakeRead(params PartnersReadModel[] items) => _items = items.ToList();
        public Task<PartnersReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<PartnersReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartnersReadModel>>(_items);
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult((long)_items.Count);
    }

    private static PartnerReadRepository CreateRepo(params PartnersReadModel[] partners)
        => new(new FakeRead(partners));

    [Fact]
    public async Task ListAsync_ReturnsOnlyMine_NotOthers_NorNull()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var repo = CreateRepo(
            new PartnersReadModel { Id = Guid.NewGuid(), Name = "Mine", ClientId = a },
            new PartnersReadModel { Id = Guid.NewGuid(), Name = "Other", ClientId = b },
            new PartnersReadModel { Id = Guid.NewGuid(), Name = "NullRow", ClientId = null });
        var list = await repo.ListAsync(a);
        Assert.Single(list);
        Assert.Equal("Mine", list[0].Name);
    }

    // BE-085: два клиента — B запрашивает партнёра A ⇒ null (404), не 403.
    [Fact]
    public async Task GetByIdAsync_OtherClientsPartner_ReturnsNull()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var id = Guid.NewGuid();
        var repo = CreateRepo(new PartnersReadModel { Id = id, Name = "A", ClientId = a });
        Assert.Null(await repo.GetByIdAsync(id, b));     // чужой → null → 404
        Assert.NotNull(await repo.GetByIdAsync(id, a));  // свой → есть
    }
}
