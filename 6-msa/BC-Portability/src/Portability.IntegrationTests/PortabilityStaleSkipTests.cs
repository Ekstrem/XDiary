using Portability.Api.Events;
using Portability.Domain.Implementation;
using Portability.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.Extensions.DependencyInjection;
using XDiary.Contracts;
using Xunit;

namespace Portability.IntegrationTests;

/// <summary>BE-140 L2: stale-skip СЃ `LegacyTimestampFloor` (РєР»Р°СЃСЃ BE-137) РґР»СЏ `csv_document_read`.</summary>
public class PortabilityStaleSkipTests
{
    private const long LegacyMs = 1_795_000_000_000L;

    private static IDomainEvent<Portability.Domain.IPortability> Event()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).ExportRecords(new[] { "id,name", "1,РђРЅСЏ" }, Guid.NewGuid());
        Assert.True(res.IsSuccess());
        return res.Event!;
    }

    private static (PortabilityEventHandler Handler, FakeReadStore Store) Build(long existingVersion)
    {
        var store = new FakeReadStore();
        var sc = new ServiceCollection();
        sc.AddSingleton<IReadModelStore<Portability.Domain.IPortability, CsvDocumentReadModel>>(store);
        sc.AddSingleton<IReadRepository<Portability.Domain.IPortability, CsvDocumentReadModel>>(new FakeReadRepo(existingVersion));
        var sp = sc.BuildServiceProvider(); // BE-140: НЕ dispose — хендлер держит IServiceScopeFactory
        return (new PortabilityEventHandler(sp.GetRequiredService<IServiceScopeFactory>()), store);
    }

    [Fact]
    public async Task LegacyMsVersion_DoesNotSwallowEvent()
    {
        var (handler, store) = Build(LegacyMs);
        await handler.HandleAsync(Event());
        Assert.NotNull(store.Upserted);
        Assert.NotEmpty(store.Upserted!.Rows);
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

    private sealed class FakeReadStore : IReadModelStore<Portability.Domain.IPortability, CsvDocumentReadModel>
    {
        public CsvDocumentReadModel? Upserted;
        public Task UpsertAsync(CsvDocumentReadModel model, CancellationToken ct = default) { Upserted = model; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReadRepo : IReadRepository<Portability.Domain.IPortability, CsvDocumentReadModel>
    {
        private readonly long _version;
        public FakeReadRepo(long version) => _version = version;
        public Task<CsvDocumentReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<CsvDocumentReadModel?>(_version == 0 ? null : new CsvDocumentReadModel { Id = id, Version = _version });
        public Task<IReadOnlyList<CsvDocumentReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CsvDocumentReadModel>>(Array.Empty<CsvDocumentReadModel>());
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }
}
