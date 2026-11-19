using DailyMetrics.Api.Events;
using DailyMetrics.Domain.Implementation;
using DailyMetrics.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;

namespace DailyMetrics.IntegrationTests;

/// <summary>BE-140 L2: stale-skip с `LegacyTimestampFloor` (класс BE-137) для `daily_metrics_read`.</summary>
public class DailyMetricsStaleSkipTests
{
    private const long LegacyMs = 1_795_000_000_000L;

    private static IDomainEvent<DailyMetrics.Domain.IDailyMetrics> Event()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).RecordStress("2026-11-19", 7, Guid.NewGuid());
        Assert.True(res.IsSuccess());
        return res.Event!;
    }

    private static (DailyMetricsEventHandler Handler, FakeReadStore Store) Build(long existingVersion)
    {
        var store = new FakeReadStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IReadModelStore<DailyMetrics.Domain.IDailyMetrics, DailyMetricsReadModel>>(store);
        sc.AddSingleton<IReadRepository<DailyMetrics.Domain.IDailyMetrics, DailyMetricsReadModel>>(new FakeReadRepo(existingVersion));
        using var sp = sc.BuildServiceProvider();
        return (new DailyMetricsEventHandler(sp.GetRequiredService<IServiceScopeFactory>()), store);
    }

    [Fact]
    public async Task LegacyMsVersion_DoesNotSwallowEvent()
    {
        var (handler, store) = Build(LegacyMs);
        await handler.HandleAsync(Event());
        Assert.NotNull(store.Upserted);
        Assert.Equal(7, store.Upserted!.Stress);
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

    private sealed class FakeReadStore : IReadModelStore<DailyMetrics.Domain.IDailyMetrics, DailyMetricsReadModel>
    {
        public DailyMetricsReadModel? Upserted;
        public Task UpsertAsync(DailyMetricsReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReadRepo : IReadRepository<DailyMetrics.Domain.IDailyMetrics, DailyMetricsReadModel>
    {
        private readonly long _version;
        public FakeReadRepo(long version) => _version = version;
        public Task<DailyMetricsReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<DailyMetricsReadModel?>(_version == 0 ? null : new DailyMetricsReadModel { Id = id, Version = _version });
        public Task<IReadOnlyList<DailyMetricsReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DailyMetricsReadModel>>(Array.Empty<DailyMetricsReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }
}
