namespace GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Abstraction;
public sealed class GarminSyncRoot : IGarminSyncRoot
{
    public Guid ClientId { get; private set; }
    public string SourceId { get; private set; } = string.Empty;
    private GarminSyncRoot() { }
    public static IGarminSyncRoot CreateInstance(Guid clientId, string sourceId) => new GarminSyncRoot { ClientId = clientId, SourceId = sourceId };
}