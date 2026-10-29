using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace GarminGateway.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата GarminSync.
/// Используются и в рантайме (in-memory кэш), и в доменных тестах.
/// </summary>
public static class GarminGatewayMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000006"),
        AggregateName = "GarminSync",
        Description = "Агрегат GarminSync: состояние интеграции с Garmin (учётка, токен, окно синка).",
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
                Name = "Credentials", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "ApiKey", TypeName = "string", IsRequired = false },
                    new() { Name = "OauthToken", TypeName = "string", IsRequired = false },
                    new() { Name = "TokenExpiry", TypeName = "string", IsRequired = false }
                }
            },
            new()
            {
                Name = "SyncConfig", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Metrics", TypeName = "string[]", IsRequired = false },
                    new() { Name = "WindowDays", TypeName = "int", IsRequired = false }
                }
            },
            new()
            {
                Name = "LastSync", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Timestamp", TypeName = "string", IsRequired = false } }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "UpsertSyncState",
                Description = "Сохранить состояние синка (upsert по sourceId).",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Credentials", "SyncConfig", "LastSync" },
                ProducedEvents = new[] { "SyncStateUpserted" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "SourceIdRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.SourceId", FailureReason = "Идентификатор источника обязателен.", FailureResult = DomainOperationResultEnum.Exception }
                }
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Configured", TriggerOperation = "UpsertSyncState" },
            new() { FromState = "Configured", ToState = "Configured", TriggerOperation = "UpsertSyncState" }
        }
    };
}
