using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;

namespace AppleHealthGateway.Application;

/// <summary>
/// DTO СЂРµР·СѓР»СЊС‚Р°С‚Р° РѕРїРµСЂР°С†РёРё РЅР°Рґ Р°РіСЂРµРіР°С‚РѕРј AppleHealthGateway.
/// </summary>
public class AppleHealthGatewayOperationResult
{
    public Guid AggregateId { get; init; }
    public long Version { get; init; }
    public string Result { get; init; } = string.Empty;
    public IEnumerable<string> Reason { get; init; } = Array.Empty<string>();
    public bool IsSuccess { get; init; }

    public static AppleHealthGatewayOperationResult Failed(Guid aggregateId, string reason) => new()
    {
        AggregateId = aggregateId,
        Result = "Exception",
        Reason = new[] { reason },
        IsSuccess = false
    };

    public static AppleHealthGatewayOperationResult From(
        Guid aggregateId,
        AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>> result) => new()
    {
        AggregateId = aggregateId,
        Version = result.Event.Version,
        Result = result.Result.ToString(),
        Reason = result.Reason,
        IsSuccess = result.Result == DomainOperationResultEnum.Success
    };
}
