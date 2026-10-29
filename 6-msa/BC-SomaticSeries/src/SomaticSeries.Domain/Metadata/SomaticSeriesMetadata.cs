using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace SomaticSeries.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата SomaticPoint.
/// Используются и в рантайме (in-memory кэш), и в доменных тестах.
/// </summary>
public static class SomaticSeriesMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000004"),
        AggregateName = "SomaticPoint",
        Description = "Агрегат SomaticPoint: минутная точка физиологии (t, hr, body_battery).",
        ValueObjects = new List<ValueObjectMetadata>
        {
            new()
            {
                Name = "Root", IsAggregateRoot = true, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "T", TypeName = "string", IsRequired = true }
                }
            },
            new()
            {
                Name = "HeartRate", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Hr", TypeName = "int", IsRequired = false } }
            },
            new()
            {
                Name = "BodyBattery", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "BodyBattery", TypeName = "int", IsRequired = false } }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "ImportPoint",
                Description = "Импортировать минутную точку (upsert по t).",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "HeartRate", "BodyBattery" },
                ProducedEvents = new[] { "SomaticImported" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "TRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.T", FailureReason = "Минутная метка t обязательна.", FailureResult = DomainOperationResultEnum.Exception }
                }
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Stored", TriggerOperation = "ImportPoint" },
            new() { FromState = "Stored", ToState = "Stored", TriggerOperation = "ImportPoint" }
        }
    };
}
