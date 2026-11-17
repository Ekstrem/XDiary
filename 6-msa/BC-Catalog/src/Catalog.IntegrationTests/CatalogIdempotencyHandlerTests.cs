using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Catalog.Application.Commands;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Xunit;

namespace Catalog.IntegrationTests;

/// <summary>
/// BE-122: AddCatalogItemCommand принимает Guid? Id — клиентский id используется как id агрегата,
/// при его отсутствии генерится новый (совместимость со старыми вызовами без id).
/// </summary>
public sealed class CatalogIdempotencyHandlerTests
{
    private sealed class FakeRepo : IRepository<ICatalog, ICatalogAnemicModel>
    {
        public ICatalogAnemicModel? Saved { get; private set; }
        public Task<ICatalogAnemicModel?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ICatalogAnemicModel?>(null);
        public Task SaveAsync(AggregateResult<ICatalog, ICatalogAnemicModel> result, CancellationToken ct = default)
        { Saved = result.Model(); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Handle_WithClientId_UsesThatId()
    {
        var repo = new FakeRepo();
        var handler = new AddCatalogItemHandler(repo);
        var id = Guid.NewGuid();
        await handler.Handle(new AddCatalogItemCommand(id, "toy", "вибратор", Guid.NewGuid()), default);
        Assert.Equal(id, repo.Saved!.Id);
    }

    [Fact]
    public async Task Handle_WithoutId_GeneratesId()
    {
        var repo = new FakeRepo();
        var handler = new AddCatalogItemHandler(repo);
        await handler.Handle(new AddCatalogItemCommand(null, "toy", "вибратор", Guid.NewGuid()), default);
        Assert.NotEqual(Guid.Empty, repo.Saved!.Id);
    }

    [Fact]
    public async Task Handle_SameIdTwice_ProducesSameAggregateId()
    {
        var repo = new FakeRepo();
        var handler = new AddCatalogItemHandler(repo);
        var id = Guid.NewGuid();
        await handler.Handle(new AddCatalogItemCommand(id, "toy", "вибратор", Guid.NewGuid()), default);
        var first = repo.Saved!.Id;
        await handler.Handle(new AddCatalogItemCommand(id, "toy", "вибратор", Guid.NewGuid()), default);
        Assert.Equal(first, repo.Saved!.Id);
    }
}
