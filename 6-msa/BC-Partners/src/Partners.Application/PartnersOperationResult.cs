using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;

namespace Partners.Application;

/// <summary>
/// DTO СЂРµР·СѓР»СЊС‚Р°С‚Р° РѕРїРµСЂР°С†РёРё РЅР°Рґ Р°РіСЂРµРіР°С‚РѕРј Partner.
/// </summary>
public class PartnersOperationResult
{
    public Guid AggregateId { get; init; }
    public long Version { get; init; }
    public string Result { get; init; } = string.Empty;
    public IEnumerable<string> Reason { get; init; } = Array.Empty<string>();
    public bool IsSuccess { get; init; }

    public static PartnersOperationResult Failed(Guid aggregateId, string reason) => new()
    {
        AggregateId = aggregateId,
        Result = "Exception",
        Reason = new[] { reason },
        IsSuccess = false
    };

    public static PartnersOperationResult From(
        Guid aggregateId,
        AggregateResult<IPartners, IAnemicModel<IPartners>> result) => new()
    {
        AggregateId = aggregateId,
        Version = result.Event.Version,
        Result = result.Result.ToString(),
        Reason = result.Reason,
        IsSuccess = result.Result == DomainOperationResultEnum.Success
    };
}
