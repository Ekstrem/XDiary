namespace AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.Domain.Abstraction;
public sealed class AppleImportRoot : IAppleImportRoot
{
    public string SourceId { get; private set; } = string.Empty;
    private AppleImportRoot() { }
    public static IAppleImportRoot CreateInstance(string sourceId) => new AppleImportRoot { SourceId = sourceId };
}