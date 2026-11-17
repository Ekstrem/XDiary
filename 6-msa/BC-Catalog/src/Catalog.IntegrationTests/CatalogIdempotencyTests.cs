using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Catalog.Domain.Implementation;
using Catalog.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.IntegrationTests;

/// <summary>
/// BE-051: идемпотентность создания. Повтор команды с тем же id → upsert по Id
/// (CatalogCommandRepository.SaveAsync) → одна запись, а не дубль.
/// </summary>
public sealed class CatalogIdempotencyTests
{
    [Fact]
    public async Task Save_SameAggregateId_Twice_KeepsSingleRecord()
    {
        var options = new DbContextOptionsBuilder<CommandDbContext>()
            .UseInMemoryDatabase($"catalog-idem-{Guid.NewGuid():N}")
            .Options;
        using var db = new CommandDbContext(options);
        var repo = new CatalogCommandRepository(db);

        var id = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        // Повтор одного и того же добавления (retry/двойной тап) — один и тот же id.
        var first = Aggregate.CreateInstance(new AnemicModel { Id = id }).AddItem("toy", "костюмчик", clientId);
        await repo.SaveAsync(first);

        var retry = Aggregate.CreateInstance(new AnemicModel { Id = id }).AddItem("toy", "костюмчик", clientId);
        await repo.SaveAsync(retry);

        Assert.Equal(1, await db.Items.CountAsync());
    }

    [Fact]
    public void AddItem_PreservesProvidedId()
    {
        var id = Guid.NewGuid();
        var r = Aggregate.CreateInstance(new AnemicModel { Id = id }).AddItem("toy", "костюмчик", Guid.NewGuid());
        Assert.Equal(id, r.Model().Id);
    }
}
