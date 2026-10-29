using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace AppleHealthGateway.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата AppleImport.
/// Используются и в рантайме (in-memory кэш), и в доменных тестах.
/// </summary>
public static class AppleHealthGatewayMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000007"),
        AggregateName = "AppleImport",
        Description = "Агрегат AppleImport: состояние импорта из Apple Health (файлы, последний импорт).",
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
                Name = "ImportConfig", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Metrics", TypeName = "string[]", IsRequired = false }
                }
            },
            new()
            {
                Name = "LastImport", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Timestamp", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "ProcessedFiles", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Names", TypeName = "string[]", IsRequired = false } }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "UpsertImportState",
                Description = "Сохранить состояние импорта (upsert по sourceId).",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "ImportConfig", "LastImport", "ProcessedFiles" },
                ProducedEvents = new[] { "ImportStateUpserted" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "SourceIdRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.SourceId", FailureReason = "Идентификатор источника обязателен.", FailureResult = DomainOperationResultEnum.Exception }
                }
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Imported", TriggerOperation = "UpsertImportState" },
            new() { FromState = "Imported", ToState = "Imported", TriggerOperation = "UpsertImportState" }
        }
    };
}
