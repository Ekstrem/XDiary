namespace AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.Domain.Abstraction;
public sealed class AppleImportRoot : IAppleImportRoot
{
    public Guid ClientId { get; private set; }
    public string SourceId { get; private set; } = string.Empty;
    private AppleImportRoot() { }
    public static IAppleImportRoot CreateInstance(Guid clientId, string sourceId) => new AppleImportRoot { ClientId = clientId, SourceId = sourceId };
}