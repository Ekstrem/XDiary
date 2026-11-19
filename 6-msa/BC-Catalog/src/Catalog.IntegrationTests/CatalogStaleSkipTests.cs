using Catalog.Api.Events;
using Catalog.Domain.Implementation;
using Catalog.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;

namespace Catalog.IntegrationTests;

/// <summary>
/// BE-140 L2: stale-skip с `LegacyTimestampFloor` (класс BE-137) для `catalog_read`.
/// </summary>
public class CatalogStaleSkipTests
{
    private const long LegacyMs = 1_795_000_000_000L;

    private static IDomainEvent<Catalog.Domain.ICatalog> Event()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", Guid.NewGuid());
        Assert.True(res.IsSuccess());
        return res.Event!;
    }

    private static (CatalogEventHandler Handler, FakeReadStore Store) Build(long existingVersion)
    {
        var store = new FakeReadStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IReadModelStore<Catalog.Domain.ICatalog, CatalogReadModel>>(store);
        sc.AddSingleton<IReadRepository<Catalog.Domain.ICatalog, CatalogReadModel>>(new FakeReadRepo(existingVersion));
        using var sp = sc.BuildServiceProvider();
        return (new CatalogEventHandler(sp.GetRequiredService<IServiceScopeFactory>()), store);
    }

    [Fact]
    public async Task LegacyMsVersion_DoesNotSwallowEvent()
    {
        var (handler, store) = Build(LegacyMs);
        await handler.HandleAsync(Event());
        Assert.NotNull(store.Upserted);
        Assert.Equal("Ball", store.Upserted!.Name);
    }

    [Fact]
    public async Task SmallerExistingVersion_SkipsStaleEvent()
    {
        var ev = Event();
        var shouldSkip = ev.Version <= 5;
        var (handler, store) = Build(existingVersion: 5);
        await handler.HandleAsync(ev);
        Assert.Equal(shouldSkip, store.Upserted is null);
    }

    [Fact]
    public async Task FirstEvent_OnEmptyRow_IsApplied()
    {
        var (handler, store) = Build(existingVersion: 0);
        await handler.HandleAsync(Event());
        Assert.NotNull(store.Upserted);
    }

    private sealed class FakeReadStore : IReadModelStore<Catalog.Domain.ICatalog, CatalogReadModel>
    {
        public CatalogReadModel? Upserted;
        public Task UpsertAsync(CatalogReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReadRepo : IReadRepository<Catalog.Domain.ICatalog, CatalogReadModel>
    {
        private readonly long _version;
        public FakeReadRepo(long version) => _version = version;
        public Task<CatalogReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<CatalogReadModel?>(_version == 0 ? null : new CatalogReadModel { Id = id, Version = _version });
        public Task<IReadOnlyList<CatalogReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CatalogReadModel>>(Array.Empty<CatalogReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }
}
