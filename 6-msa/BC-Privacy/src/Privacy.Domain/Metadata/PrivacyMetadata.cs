using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace Privacy.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата PinCredential.
/// Используются и в рантайме (in-memory кэш), и в доменных тестах.
/// </summary>
public static class PrivacyMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000009"),
        AggregateName = "PinCredential",
        Description = "Агрегат PinCredential: хеш PIN (PBKDF2-SHA256) с солью. Открытый PIN не хранится.",
        ValueObjects = new List<ValueObjectMetadata>
        {
            new()
            {
                Name = "Root", IsAggregateRoot = true, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "UserId", TypeName = "string", IsRequired = true }
                }
            },
            new()
            {
                Name = "Salt", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Salt", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Hash", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Hash", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Iterations", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Iterations", TypeName = "int", IsRequired = false } }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "SetPin",
                Description = "Установить PIN (upsert по UserId).",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Salt", "Hash", "Iterations" },
                ProducedEvents = new[] { "PinSet" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "UserIdRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.UserId", FailureReason = "Идентификатор пользователя обязателен.", FailureResult = DomainOperationResultEnum.Exception }
                }
            },
            new()
            {
                CommandName = "RemovePin",
                Description = "Удалить PIN.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root" },
                ProducedEvents = new[] { "PinRemoved" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustExist", Type = InvariantType.Assertion, RuleExpression = "IsNotNewEntity", FailureReason = "PIN не установлен.", FailureResult = DomainOperationResultEnum.Exception }
                }
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Set", TriggerOperation = "SetPin" },
            new() { FromState = "Set", ToState = "Set", TriggerOperation = "SetPin" },
            new() { FromState = "Set", ToState = "Removed", TriggerOperation = "RemovePin" }
        }
    };
}
