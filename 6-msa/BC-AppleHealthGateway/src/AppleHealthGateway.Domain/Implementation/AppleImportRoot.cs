namespace AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.Domain.Abstraction;
public sealed record AppleImportRoot(Guid ClientId, string SourceId) : IAppleImportRoot
{
    public static IAppleImportRoot CreateInstance(Guid clientId, string sourceId) => new AppleImportRoot(clientId, sourceId);
}
