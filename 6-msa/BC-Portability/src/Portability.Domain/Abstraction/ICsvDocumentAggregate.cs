namespace Portability.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;

/// <summary>Агрегат Portability: состояние (анемичная модель) + бизнес-операции.</summary>
public interface ICsvDocumentAggregate : IAggregate<IPortability>, ICsvDocumentAnemicModel
{
    ICsvDocumentRoot Root { get; }
    IReadOnlyList<string> Rows { get; }
    string Kind { get; }
    Guid? ClientId { get; }
    AggregateResult<IPortability, ICsvDocumentAnemicModel> ExportRecords(IReadOnlyList<string> rows, Guid clientId);
    AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportRecords(IReadOnlyList<string> rows, Guid clientId);
    AggregateResult<IPortability, ICsvDocumentAnemicModel> ImportSomatic(IReadOnlyList<string> rows, Guid clientId);
}
