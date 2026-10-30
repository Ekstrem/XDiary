namespace Partners.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class CommandDbContextFactory : IDesignTimeDbContextFactory<CommandDbContext>
{
    public CommandDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CommandDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=design_time;Username=postgres;Password=postgres")
            .Options;
        return new CommandDbContext(options);
    }
}