namespace GarminGateway.Domain.Implementation;
using GarminGateway.Domain.Abstraction;
public sealed class GarminSyncRoot : IGarminSyncRoot
{
    public string SourceId { get; private set; } = string.Empty;
    private GarminSyncRoot() { }
    public static IGarminSyncRoot CreateInstance(string sourceId) => new GarminSyncRoot { SourceId = sourceId };
}