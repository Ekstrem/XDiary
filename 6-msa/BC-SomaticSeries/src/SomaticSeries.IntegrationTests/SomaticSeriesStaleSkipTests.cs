using SomaticSeries.Api.Events;
using SomaticSeries.Domain.Implementation;
using SomaticSeries.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;

namespace SomaticSeries.IntegrationTests;

/// <summary>BE-140 L2: stale-skip с `LegacyTimestampFloor` (класс BE-137) для `somatic_read`.</summary>
public class SomaticSeriesStaleSkipTests
{
    private const long LegacyMs = 1_795_000_000_000L;

    private static IDomainEvent<SomaticSeries.Domain.ISomaticSeries> Event()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).RecordPoint("2026-11-19T10:00", 62, 45, Guid.NewGuid());
        Assert.True(res.IsSuccess());
        return res.Event!;
    }

    private static (SomaticSeriesEventHandler Handler, FakeReadStore Store) Build(long existingVersion)
    {
        var store = new FakeReadStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IReadModelStore<SomaticSeries.Domain.ISomaticSeries, SomaticSeriesReadModel>>(store);
        sc.AddSingleton<IReadRepository<SomaticSeries.Domain.ISomaticSeries, SomaticSeriesReadModel>>(new FakeReadRepo(existingVersion));
        using var sp = sc.BuildServiceProvider();
        return (new SomaticSeriesEventHandler(sp.GetRequiredService<IServiceScopeFactory>()), store);
    }

    [Fact]
    public async Task LegacyMsVersion_DoesNotSwallowEvent()
    {
        var (handler, store) = Build(LegacyMs);
        await handler.HandleAsync(Event());
        Assert.NotNull(store.Upserted);
        Assert.Equal(62, store.Upserted!.Hr);
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

    private sealed class FakeReadStore : IReadModelStore<SomaticSeries.Domain.ISomaticSeries, SomaticSeriesReadModel>
    {
        public SomaticSeriesReadModel? Upserted;
        public Task UpsertAsync(SomaticSeriesReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReadRepo : IReadRepository<SomaticSeries.Domain.ISomaticSeries, SomaticSeriesReadModel>
    {
        private readonly long _version;
        public FakeReadRepo(long version) => _version = version;
        public Task<SomaticSeriesReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<SomaticSeriesReadModel?>(_version == 0 ? null : new SomaticSeriesReadModel { Id = id, Version = _version });
        public Task<IReadOnlyList<SomaticSeriesReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SomaticSeriesReadModel>>(Array.Empty<SomaticSeriesReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }
}
