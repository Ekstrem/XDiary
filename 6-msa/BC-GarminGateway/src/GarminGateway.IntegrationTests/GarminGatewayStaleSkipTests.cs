using GarminGateway.Api.Events;
using GarminGateway.Domain.Implementation;
using GarminGateway.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;

namespace GarminGateway.IntegrationTests;

/// <summary>BE-140 L2: stale-skip СЃ `LegacyTimestampFloor` (РєР»Р°СЃСЃ BE-137) РґР»СЏ `garmin_sync_read`.</summary>
public class GarminGatewayStaleSkipTests
{
    private const long LegacyMs = 1_795_000_000_000L;

    private static IDomainEvent<GarminGateway.Domain.IGarminGateway> Event()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).Connect(Guid.NewGuid(), new Credentials("key-1", "token-1", "2026-12-31"));
        Assert.True(res.IsSuccess());
        return res.Event!;
    }

    private static (GarminGatewayEventHandler Handler, FakeReadStore Store) Build(long existingVersion)
    {
        var store = new FakeReadStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IReadModelStore<GarminGateway.Domain.IGarminGateway, GarminSyncReadModel>>(store);
        sc.AddSingleton<IReadRepository<GarminGateway.Domain.IGarminGateway, GarminSyncReadModel>>(new FakeReadRepo(existingVersion));
        var sp = sc.BuildServiceProvider(); // BE-140: НЕ dispose — хендлер держит IServiceScopeFactory
        return (new GarminGatewayEventHandler(sp.GetRequiredService<IServiceScopeFactory>()), store);
    }

    [Fact]
    public async Task LegacyMsVersion_DoesNotSwallowEvent()
    {
        var (handler, store) = Build(LegacyMs);
        await handler.HandleAsync(Event());
        Assert.NotNull(store.Upserted);
        Assert.Equal("key-1", store.Upserted!.ApiKey);
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

    private sealed class FakeReadStore : IReadModelStore<GarminGateway.Domain.IGarminGateway, GarminSyncReadModel>
    {
        public GarminSyncReadModel? Upserted;
        public Task UpsertAsync(GarminSyncReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReadRepo : IReadRepository<GarminGateway.Domain.IGarminGateway, GarminSyncReadModel>
    {
        private readonly long _version;
        public FakeReadRepo(long version) => _version = version;
        public Task<GarminSyncReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<GarminSyncReadModel?>(_version == 0 ? null : new GarminSyncReadModel { Id = id, Version = _version });
        public Task<IReadOnlyList<GarminSyncReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GarminSyncReadModel>>(Array.Empty<GarminSyncReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }
}
