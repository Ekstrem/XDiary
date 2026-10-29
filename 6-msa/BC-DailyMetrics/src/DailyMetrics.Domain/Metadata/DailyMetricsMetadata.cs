using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace DailyMetrics.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата DailyMetrics.
/// Используются и в рантайме (in-memory кэш), и в доменных тестах.
/// </summary>
public static class DailyMetricsMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000005"),
        AggregateName = "DailyMetrics",
        Description = "Агрегат DailyMetrics: дневная сводка физиологии (стресс/пульс покоя/HRV) + сон.",
        ValueObjects = new List<ValueObjectMetadata>
        {
            new()
            {
                Name = "Root", IsAggregateRoot = true, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Date", TypeName = "string", IsRequired = true }
                }
            },
            new()
            {
                Name = "Stress", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Stress", TypeName = "int", IsRequired = false } }
            },
            new()
            {
                Name = "RestingHR", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "RestingHr", TypeName = "int", IsRequired = false } }
            },
            new()
            {
                Name = "Hrv", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Hrv", TypeName = "int", IsRequired = false } }
            },
            new()
            {
                Name = "Sleep", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Score", TypeName = "int", IsRequired = false },
                    new() { Name = "DurationMin", TypeName = "int", IsRequired = false },
                    new() { Name = "DeepMin", TypeName = "int", IsRequired = false },
                    new() { Name = "LightMin", TypeName = "int", IsRequired = false },
                    new() { Name = "RemMin", TypeName = "int", IsRequired = false },
                    new() { Name = "AwakeMin", TypeName = "int", IsRequired = false }
                }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "ImportDaily",
                Description = "Импортировать дневную сводку (upsert по date).",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Stress", "RestingHR", "Hrv", "Sleep" },
                ProducedEvents = new[] { "DailyImported" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "DateRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.Date", FailureReason = "Дата обязательна.", FailureResult = DomainOperationResultEnum.Exception }
                }
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Stored", TriggerOperation = "ImportDaily" },
            new() { FromState = "Stored", ToState = "Stored", TriggerOperation = "ImportDaily" }
        }
    };
}
