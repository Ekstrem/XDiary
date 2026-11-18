namespace GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Abstraction;
internal sealed record GarminSyncRoot(Guid ClientId, string SourceId) : IGarminSyncRoot
{
    public static IGarminSyncRoot CreateInstance(Guid clientId, string sourceId) => new GarminSyncRoot(clientId, sourceId);
}