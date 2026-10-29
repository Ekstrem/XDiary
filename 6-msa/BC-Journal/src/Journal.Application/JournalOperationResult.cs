using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;

namespace Journal.Application;

/// <summary>
/// DTO результата операции над агрегатом Record.
/// </summary>
public class JournalOperationResult
{
    public Guid AggregateId { get; init; }
    public long Version { get; init; }
    public string Result { get; init; } = string.Empty;
    public IEnumerable<string> Reason { get; init; } = Array.Empty<string>();
    public bool IsSuccess { get; init; }

    public static JournalOperationResult Failed(Guid aggregateId, string reason) => new()
    {
        AggregateId = aggregateId,
        Result = "Exception",
        Reason = new[] { reason },
        IsSuccess = false
    };

    public static JournalOperationResult From(
        Guid aggregateId,
        AggregateResult<IJournal, IAnemicModel<IJournal>> result) => new()
    {
        AggregateId = aggregateId,
        Version = result.Event.Version,
        Result = result.Result.ToString(),
        Reason = result.Reason,
        IsSuccess = result.Result == DomainOperationResultEnum.Success
    };
}
