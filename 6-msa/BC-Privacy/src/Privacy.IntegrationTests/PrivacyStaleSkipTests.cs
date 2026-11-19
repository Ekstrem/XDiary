using Privacy.Api.Events;
using Privacy.Domain.Implementation;
using Privacy.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;

namespace Privacy.IntegrationTests;

/// <summary>BE-140 L2: stale-skip СЃ `LegacyTimestampFloor` (РєР»Р°СЃСЃ BE-137) РґР»СЏ `pin_credential_read`.</summary>
public class PrivacyStaleSkipTests
{
    private const long LegacyMs = 1_795_000_000_000L;

    private static IDomainEvent<Privacy.Domain.IPrivacy> Event()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).SetPin(Guid.NewGuid(), "salt", "hash", 100_000);
        Assert.True(res.IsSuccess());
        return res.Event!;
    }

    private static (PrivacyEventHandler Handler, FakeReadStore Store) Build(long existingVersion)
    {
        var store = new FakeReadStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IReadModelStore<Privacy.Domain.IPrivacy, PinCredentialReadModel>>(store);
        sc.AddSingleton<IReadRepository<Privacy.Domain.IPrivacy, PinCredentialReadModel>>(new FakeReadRepo(existingVersion));
        var sp = sc.BuildServiceProvider(); // BE-140: НЕ dispose — хендлер держит IServiceScopeFactory
        return (new PrivacyEventHandler(sp.GetRequiredService<IServiceScopeFactory>()), store);
    }

    [Fact]
    public async Task LegacyMsVersion_DoesNotSwallowEvent()
    {
        var (handler, store) = Build(LegacyMs);
        await handler.HandleAsync(Event());
        Assert.NotNull(store.Upserted);
        Assert.Equal("salt", store.Upserted!.Salt);
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

    private sealed class FakeReadStore : IReadModelStore<Privacy.Domain.IPrivacy, PinCredentialReadModel>
    {
        public PinCredentialReadModel? Upserted;
        public Task UpsertAsync(PinCredentialReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReadRepo : IReadRepository<Privacy.Domain.IPrivacy, PinCredentialReadModel>
    {
        private readonly long _version;
        public FakeReadRepo(long version) => _version = version;
        public Task<PinCredentialReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<PinCredentialReadModel?>(_version == 0 ? null : new PinCredentialReadModel { Id = id, Version = _version });
        public Task<IReadOnlyList<PinCredentialReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PinCredentialReadModel>>(Array.Empty<PinCredentialReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }
}
