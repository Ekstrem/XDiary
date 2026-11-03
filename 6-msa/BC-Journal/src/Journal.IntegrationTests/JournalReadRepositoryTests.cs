using Microsoft.EntityFrameworkCore;
using Journal.Storage;
using Xunit;

public class JournalReadRepositoryTests
{
    private static JournalReadRepository CreateRepo(params RecordEntity[] records)
    {
        var opts = new DbContextOptionsBuilder<ReadDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ReadDbContext(opts);
        db.Records.AddRange(records);
        db.SaveChanges();
        return new JournalReadRepository(db);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyMine_NotOthers_NorNull()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var repo = CreateRepo(
            new RecordEntity { Id = Guid.NewGuid(), ClientId = a, StartedAt = "2026-11-09T10:00:00" },
            new RecordEntity { Id = Guid.NewGuid(), ClientId = b, StartedAt = "2026-11-09T11:00:00" },
            new RecordEntity { Id = Guid.NewGuid(), ClientId = null, StartedAt = "2026-11-09T12:00:00" });
        var list = await repo.ListAsync(a, null, null);
        Assert.Single(list);
        Assert.Equal("2026-11-09T10:00:00", list[0].StartedAt);
    }
}