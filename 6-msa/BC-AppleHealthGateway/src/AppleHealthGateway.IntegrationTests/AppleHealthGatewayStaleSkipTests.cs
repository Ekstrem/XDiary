using AppleHealthGateway.Api.Events;
using AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;

namespace AppleHealthGateway.IntegrationTests;

/// <summary>BE-140 L2: stale-skip с `LegacyTimestampFloor` (класс BE-137) для `apple_health_read`.</summary>
public class AppleHealthGatewayStaleSkipTests
{
    private const long LegacyMs = 1_795_000_000_000L;

    private static IDomainEvent<AppleHealthGateway.Domain.IAppleHealthGateway> Event()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).Configure(Guid.NewGuid(), new[] { "steps", "hr" });
        Assert.True(res.IsSuccess());
        return res.Event!;
    }

    private static (AppleHealthGatewayEventHandler Handler, FakeReadStore Store) Build(long existingVersion)
    {
        var store = new FakeReadStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IReadModelStore<AppleHealthGateway.Domain.IAppleHealthGateway, AppleImportReadModel>>(store);
        sc.AddSingleton<IReadRepository<AppleHealthGateway.Domain.IAppleHealthGateway, AppleImportReadModel>>(new FakeReadRepo(existingVersion));
        using var sp = sc.BuildServiceProvider();
        return (new AppleHealthGatewayEventHandler(sp.GetRequiredService<IServiceScopeFactory>()), store);
    }

    [Fact]
    public async Task LegacyMsVersion_DoesNotSwallowEvent()
    {
        var (handler, store) = Build(LegacyMs);
        await handler.HandleAsync(Event());
        Assert.NotNull(store.Upserted);
        Assert.NotEmpty(store.Upserted!.Metrics);
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

    private sealed class FakeReadStore : IReadModelStore<AppleHealthGateway.Domain.IAppleHealthGateway, AppleImportReadModel>
    {
        public AppleImportReadModel? Upserted;
        public Task UpsertAsync(AppleImportReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReadRepo : IReadRepository<AppleHealthGateway.Domain.IAppleHealthGateway, AppleImportReadModel>
    {
        private readonly long _version;
        public FakeReadRepo(long version) => _version = version;
        public Task<AppleImportReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<AppleImportReadModel?>(_version == 0 ? null : new AppleImportReadModel { Id = id, Version = _version });
        public Task<IReadOnlyList<AppleImportReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AppleImportReadModel>>(Array.Empty<AppleImportReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }
}
