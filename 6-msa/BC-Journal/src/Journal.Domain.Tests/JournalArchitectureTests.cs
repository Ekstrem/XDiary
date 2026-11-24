using System.Reflection;
using Journal.Domain.Abstraction;
using Journal.InternalContracts;
using Xunit;

// BE-151 (DS ArchitectureTests): границы слоёв Journal и отсутствие заглушек.
public class JournalArchitectureTests
{
    private static readonly string[] ForbiddenReferences =
    [
        "Microsoft.EntityFrameworkCore", "MediatR", "Autofac", "Journal.Api",
        "Journal.Application", "Journal.Storage", "Microsoft.AspNetCore"
    ];

    [Fact]
    public void Domain_When_ReferencesInspected_Should_NotDependOnInfrastructure()
    {
        var references = typeof(IJournalAnemicModel).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.DoesNotContain(references, r => ForbiddenReferences.Any(f => r.StartsWith(f, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void InternalContracts_When_ReferencesInspected_Should_NotDependOnApiOrStorage()
    {
        var references = typeof(JournalReadModel).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.DoesNotContain(references, r => ForbiddenReferences.Any(f => r.StartsWith(f, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ProductCode_When_Scanned_Should_NotContainNotImplementedException()
    {
        var root = LocateSourceRoot();
        var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains("Tests", StringComparison.OrdinalIgnoreCase))
            .Where(f => File.ReadAllText(f).Contains("throw new NotImplementedException", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(offenders);
    }

    private static string LocateSourceRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(JournalArchitectureTests).Assembly.Location)!);
        while (dir is not null && !dir.GetFiles("Journal.sln").Any()) dir = dir.Parent!;
        Assert.True(dir is not null, "Journal.sln не найден выше по дереву от тестовой сборки");
        return dir.FullName;
    }
}
