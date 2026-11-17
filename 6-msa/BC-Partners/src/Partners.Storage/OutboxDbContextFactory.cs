namespace Partners.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class OutboxDbContextFactory : IDesignTimeDbContextFactory<OutboxDbContext>
{
    public OutboxDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OutboxDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=design_time;Username=postgres;Password=postgres")
            .Options;
        return new OutboxDbContext(options);
    }
}