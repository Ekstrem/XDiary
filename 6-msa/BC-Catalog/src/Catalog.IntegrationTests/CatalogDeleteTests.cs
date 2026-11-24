using Catalog.Api.Events;
using Catalog.Application.Queries;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;
using Catalog.InternalContracts;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;

namespace Catalog.IntegrationTests;

/// <summary>BE-147: после Remove read-модель не отдаёт позицию ни по id, ни в списке.</summary>
public class CatalogDeleteTests
{
    [Fact]
    public void Remove_WhenCalled_ShouldSetDeletedAtInEvent()
    {
        var created = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", Guid.NewGuid());
        var removed = Aggregate.CreateInstance(created.Model()).Remove();

        Assert.NotNull(removed.Model().DeletedAt);
        Assert.True(removed.Event!.ChangedValueObjects.ContainsKey("DeletedAt"));
    }

    [Fact]
    public void Remove_WhenCalledTwice_ShouldKeepOriginalDeletedAt()
    {
        var created = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", Guid.NewGuid());
        var first = Aggregate.CreateInstance(created.Model()).Remove();
        var second = Aggregate.CreateInstance(first.Model()).Remove();

        Assert.Equal(first.Model().DeletedAt, second.Model().DeletedAt);
    }

    [Fact]
    public async Task Projection_WhenDeletedAtPresent_ShouldDeleteReadModel()
    {
        var created = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", Guid.NewGuid());
        var removed = Aggregate.CreateInstance(created.Model()).Remove();
        var store = new FakeReadStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IReadModelStore<ICatalog, CatalogReadModel>>(store);
        sc.AddSingleton<IReadRepository<ICatalog, CatalogReadModel>>(new FakeReadRepo(null));
        using var sp = sc.BuildServiceProvider();
        var handler = new CatalogEventHandler(sp.GetRequiredService<IServiceScopeFactory>());

        await handler.HandleAsync(removed.Event!);

        Assert.Equal(removed.Event!.Id, store.DeletedId);
        Assert.Null(store.Upserted);
    }

    [Fact]
    public async Task ListAndGetById_WhenDeletedAtSet_ShouldNotReturnItem()
    {
        var clientId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        var repo = new FakeReadRepo(new List<CatalogReadModel>
        {
            new() { Id = deletedId, ClientId = clientId, DeletedAt = DateTime.UtcNow },
            new() { Id = activeId, ClientId = clientId },
        });
        var listHandler = new ListCatalogItemsHandler(repo);
        var getHandler = new GetCatalogItemByIdHandler(repo);

        var list = await listHandler.Handle(new ListCatalogItemsQuery(clientId), default);
        var deleted = await getHandler.Handle(new GetCatalogItemByIdQuery(deletedId, clientId), default);

        Assert.Single(list);
        Assert.Equal(activeId, list[0].Id);
        Assert.Null(deleted);
    }

    private sealed class FakeReadStore : IReadModelStore<ICatalog, CatalogReadModel>
    {
        public CatalogReadModel? Upserted;
        public Guid? DeletedId;
        public Task UpsertAsync(CatalogReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { DeletedId = id; return Task.CompletedTask; }
    }

    private sealed class FakeReadRepo : IReadRepository<ICatalog, CatalogReadModel>
    {
        private readonly List<CatalogReadModel> _items;
        public FakeReadRepo(List<CatalogReadModel>? items) => _items = items ?? new List<CatalogReadModel>();
        public Task<CatalogReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<CatalogReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CatalogReadModel>>(_items);
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult((long)_items.Count);
    }
}
