using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.Domain.Tests;
using Journal.InternalContracts;
using Journal.Storage.ReadModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Journal.IntegrationTests;

/// <summary>
/// Интеграционный тест: Command → Event → Projection → Read Model → Assert (Query).
/// Профиль Single, in-process (InMemory read store), без внешней БД.
/// </summary>
public sealed class JournalProjectionIntegrationTest
{
    [Fact]
    public void CompleteCommand_ProjectsToReadModel_ForQuery()
    {
        // 1. Command: построить агрегат и выполнить Complete (визард завершён)
        var id = Guid.NewGuid();
        var aggregate = JournalMetadataHelper.CreateAggregate(RecordBuilder.ExistingDraftModel(id));
        var model = RecordBuilder.Model(id, 1, "Complete", RecordBuilder.CompleteVos(new[] { "partner-1" }, "2026-10-29T21:00"));
        var result = aggregate.Complete(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);

        // 2. Projection: доменное событие → Read Model (через реальный ProjectionHandler)
        var store = new FakeReadStore();
        var projection = new JournalProjectionHandler(store, new FakeReadRepository(store), NullLogger<JournalProjectionHandler>.Instance);
        projection.ProjectAsync(result.Event, CancellationToken.None).GetAwaiter().GetResult();

        // 3. Query → Assert: read model содержит данные для журнала
        var readModel = store.Models[id];
        Assert.Equal(new[] { "partner-1" }, readModel.Partners);
        Assert.Equal("2026-10-29T21:00", readModel.StartedAt);
    }
}

/// <summary>In-memory реализация Read Store (только для тестов).</summary>
internal sealed class FakeReadStore : IReadModelStore<IJournal, JournalReadModel>
{
    public readonly Dictionary<Guid, JournalReadModel> Models = new();

    public Task UpsertAsync(JournalReadModel model, CancellationToken ct = default)
    {
        Models[model.Id] = model;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Models.Remove(id);
        return Task.CompletedTask;
    }
}

/// <summary>In-memory реализация Read Repository (только для тестов).</summary>
internal sealed class FakeReadRepository : IReadRepository<IJournal, JournalReadModel>
{
    private readonly FakeReadStore _store;

    public FakeReadRepository(FakeReadStore store) => _store = store;

    public Task<JournalReadModel> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.Models.TryGetValue(id, out var model) ? model : null!);

    public Task<IReadOnlyList<JournalReadModel>> GetAllAsync(IPaging paging, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<JournalReadModel>>(_store.Models.Values.ToList());

    public Task<long> CountAsync(CancellationToken ct = default)
        => Task.FromResult((long)_store.Models.Count);
}
