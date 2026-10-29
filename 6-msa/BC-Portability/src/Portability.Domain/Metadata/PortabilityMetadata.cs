using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace Portability.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата CsvDocument.
/// Используются и в рантайме (in-memory кэш), и в доменных тестах.
/// </summary>
public static class PortabilityMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000010"),
        AggregateName = "CsvDocument",
        Description = "Агрегат CsvDocument: CSV-документ переноса (фиксированные заголовки + строки).",
        ValueObjects = new List<ValueObjectMetadata>
        {
            new()
            {
                Name = "Root", IsAggregateRoot = true, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "SourceId", TypeName = "string", IsRequired = true }
                }
            },
            new()
            {
                Name = "Headers", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Headers", TypeName = "string[]", IsRequired = false } }
            },
            new()
            {
                Name = "Rows", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Rows", TypeName = "string[]", IsRequired = false } }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "UpsertDocument",
                Description = "Сохранить CSV-документ (upsert по sourceId).",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Headers", "Rows" },
                ProducedEvents = new[] { "DocumentUpserted" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "SourceIdRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.SourceId", FailureReason = "Идентификатор документа обязателен.", FailureResult = DomainOperationResultEnum.Exception }
                }
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Stored", TriggerOperation = "UpsertDocument" },
            new() { FromState = "Stored", ToState = "Stored", TriggerOperation = "UpsertDocument" }
        }
    };
}
