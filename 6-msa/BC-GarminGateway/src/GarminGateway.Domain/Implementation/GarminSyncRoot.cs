namespace GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Abstraction;
public sealed record GarminSyncRoot(Guid ClientId, string SourceId) : IGarminSyncRoot
{
    public static IGarminSyncRoot CreateInstance(Guid clientId, string sourceId) => new GarminSyncRoot(clientId, sourceId);
}