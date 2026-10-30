namespace GarminGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IGarminGatewayAnemicModel : IAnemicModel<IGarminGateway>
{
    IGarminSyncRoot Root { get; }
    ICredentials Credentials { get; }
    ISyncConfig SyncConfig { get; }
    string LastSync { get; }
}