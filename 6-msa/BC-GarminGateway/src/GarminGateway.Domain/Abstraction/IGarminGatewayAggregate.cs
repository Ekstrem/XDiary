namespace GarminGateway.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;

/// <summary>Агрегат GarminGateway: состояние (анемичная модель) + бизнес-операции.</summary>
public interface IGarminGatewayAggregate : IAggregate<IGarminGateway>, IGarminGatewayAnemicModel
{
    IGarminSyncRoot Root { get; }
    ICredentials Credentials { get; }
    ISyncConfig SyncConfig { get; }
    string LastSync { get; }
    AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Connect(Guid clientId, ICredentials credentials, CommandToAggregate? command = null);
    AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Sync(ISyncConfig config, CommandToAggregate? command = null);
    AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> FetchActivities(CommandToAggregate? command = null);
}
