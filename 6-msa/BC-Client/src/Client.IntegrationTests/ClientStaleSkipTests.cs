using Client.Api.Events;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;

namespace Client.IntegrationTests;

/// <summary>
/// BE-140 L2: stale-skip с `LegacyTimestampFloor` (класс BE-137).
/// Legacy-строка read-модели хранит ms-таймстамп (1.79e12), а события нумеруются 1,2,3…
/// Без пола `existing.Version &gt;= event.Version` навсегда отбрасывает события.
/// </summary>
public class ClientStaleSkipTests
{
    private const string Phone = "+79000000000";
    private const long LegacyMs = 1_795_000_000_000L;

    private static IDomainEvent<IClients> VerifyEvent()
    {
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register(Phone, "123456");
        var ver = Aggregate.CreateInstance(reg.Model()).VerifyOtp("123456");
        Assert.True(ver.IsSuccess(), "verify должен пройти");
        return ver.Event!;
    }

    private static (ClientEventHandler Handler, FakeReadStore Store) Build(long existingVersion)
    {
        var store = new FakeReadStore();
        var repo = new FakeReadRepo(existingVersion);
        var sc = new ServiceCollection();
        sc.AddSingleton<IReadModelStore<IClients, ClientReadModel>>(store);
        sc.AddSingleton<IReadRepository<IClients, ClientReadModel>>(repo);
        using var sp = sc.BuildServiceProvider();
        return (new ClientEventHandler(sp.GetRequiredService<IServiceScopeFactory>()), store);
    }

    [Fact]
    public async Task LegacyMsVersion_DoesNotSwallowSmallEvent()
    {
        var ev = VerifyEvent();
        var (handler, store) = Build(LegacyMs);

        await handler.HandleAsync(ev);

        Assert.NotNull(store.Upserted);
        Assert.True(store.Upserted!.Verified, "событие verify должно примениться поверх legacy ms-версии");
    }

    [Fact]
    public async Task SmallerExistingVersion_SkipsStaleEvent()
    {
        var ev = VerifyEvent();
        // existing (5) — маленькая версия; skip ожидается только если она >= версии события.
        var shouldSkip = ev.Version <= 5;
        var (handler, store) = Build(existingVersion: 5);

        await handler.HandleAsync(ev);

        Assert.Equal(shouldSkip, store.Upserted is null);
    }

    [Fact]
    public async Task FirstEvent_OnEmptyRow_IsApplied()
    {
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register(Phone, "123456");
        var (handler, store) = Build(existingVersion: 0);

        await handler.HandleAsync(reg.Event!);

        Assert.NotNull(store.Upserted);
        Assert.Equal(Phone, store.Upserted!.Phone);
    }

    private sealed class FakeReadStore : IReadModelStore<IClients, ClientReadModel>
    {
        public ClientReadModel? Upserted;
        public Task UpsertAsync(ClientReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReadRepo : IReadRepository<IClients, ClientReadModel>
    {
        private readonly long _version;
        public FakeReadRepo(long version) => _version = version;
        public Task<ClientReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<ClientReadModel?>(_version == 0 ? null : new ClientReadModel { Id = id, Version = _version });
        public Task<IReadOnlyList<ClientReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ClientReadModel>>(Array.Empty<ClientReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }
}
