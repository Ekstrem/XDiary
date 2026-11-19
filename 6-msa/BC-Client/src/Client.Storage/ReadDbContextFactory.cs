using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Client.Storage;

/// <summary>
/// BE-135: design-time фабрика для миграций ReadDbContext (client_read).
/// Без неё `dotnet ef migrations add` не может создать контекст (регистрация AddDbContext<ReadDbContext> убрана из Program.cs в #2878).
/// </summary>
public sealed class ReadDbContextFactory : IDesignTimeDbContextFactory<ReadDbContext>
{
    public ReadDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReadDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=xdiary_test;Username=postgres")
            .Options;
        return new ReadDbContext(options);
    }
}
