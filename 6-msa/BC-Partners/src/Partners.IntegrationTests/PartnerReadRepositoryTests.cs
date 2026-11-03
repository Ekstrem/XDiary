using Microsoft.EntityFrameworkCore;
using Partners.Storage;
using Xunit;

public class PartnerReadRepositoryTests
{
    private static PartnerReadRepository CreateRepo(params PartnerEntity[] partners)
    {
        var opts = new DbContextOptionsBuilder<ReadDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ReadDbContext(opts);
        db.Partners.AddRange(partners);
        db.SaveChanges();
        return new PartnerReadRepository(db);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyMine_NotOthers_NorNull()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var repo = CreateRepo(
            new PartnerEntity { Id = Guid.NewGuid(), Name = "Mine", ClientId = a },
            new PartnerEntity { Id = Guid.NewGuid(), Name = "Other", ClientId = b },
            new PartnerEntity { Id = Guid.NewGuid(), Name = "NullRow", ClientId = null });
        var list = await repo.ListAsync(a);
        Assert.Single(list);
        Assert.Equal("Mine", list[0].Name);
    }
}