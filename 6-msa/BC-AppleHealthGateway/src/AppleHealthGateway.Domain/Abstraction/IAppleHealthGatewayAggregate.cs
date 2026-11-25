namespace AppleHealthGateway.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;

/// <summary>Агрегат AppleHealthGateway: состояние (анемичная модель) + бизнес-операции.</summary>
public interface IAppleHealthGatewayAggregate : IAggregate<IAppleHealthGateway>, IAppleHealthGatewayAnemicModel
{
    IAppleImportRoot Root { get; }
    IImportConfig ImportConfig { get; }
    string LastImport { get; }
    IReadOnlyList<string> ProcessedFiles { get; }
    AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Configure(Guid clientId, IEnumerable<string> metrics, CommandToAggregate? command = null);
    AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> ImportFile(string fileName, CommandToAggregate? command = null);
}
