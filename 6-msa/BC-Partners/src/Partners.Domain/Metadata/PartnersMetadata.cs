using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace Partners.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата Partner (эквивалент seed_Partners_metadata.sql).
/// Используются и в рантайме (in-memory кэш), и в доменных тестах.
/// </summary>
public static class PartnersMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000002"),
        AggregateName = "Partner",
        Description = "Агрегат Partner: профиль партнёра (имя, фото, пол, заметки).",
        ValueObjects = new List<ValueObjectMetadata>
        {
            new()
            {
                Name = "Root", IsAggregateRoot = true, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Name", TypeName = "string", IsRequired = true }
                }
            },
            new()
            {
                Name = "Photo", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "DataUrl", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Gender", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Value", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Notes", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Text", TypeName = "string", IsRequired = false } }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "CreatePartner",
                Description = "Создать профиль партнёра.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Photo", "Gender", "Notes" },
                ProducedEvents = new[] { "PartnerCreated" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustBeNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNewEntity", FailureReason = "Партнёр уже существует: создание невозможно.", FailureResult = DomainOperationResultEnum.Exception },
                    new() { Name = "NameRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.Name", FailureReason = "Имя партнёра обязательно.", FailureResult = DomainOperationResultEnum.Exception }
                }
            },
            new()
            {
                CommandName = "UpdatePartner",
                Description = "Отредактировать профиль партнёра.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Photo", "Gender", "Notes" },
                ProducedEvents = new[] { "PartnerUpdated" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustNotBeNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNotNewEntity", FailureReason = "Партнёр не найден для редактирования.", FailureResult = DomainOperationResultEnum.Exception },
                    new() { Name = "RootMustExist", Type = InvariantType.Assertion, RuleExpression = "VOExists:Root", FailureReason = "Корень партнёра не найден.", FailureResult = DomainOperationResultEnum.Exception }
                }
            },
            new()
            {
                CommandName = "DeletePartner",
                Description = "Удалить партнёра.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = Array.Empty<string>(),
                ProducedEvents = new[] { "PartnerDeleted" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustNotBeNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNotNewEntity", FailureReason = "Партнёр не найден для удаления.", FailureResult = DomainOperationResultEnum.Exception },
                    new() { Name = "RootMustExist", Type = InvariantType.Assertion, RuleExpression = "VOExists:Root", FailureReason = "Корень партнёра не найден.", FailureResult = DomainOperationResultEnum.Exception }
                }
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Active", TriggerOperation = "CreatePartner" },
            new() { FromState = "Active", ToState = "Active", TriggerOperation = "UpdatePartner" },
            new() { FromState = "Active", ToState = "Deleted", TriggerOperation = "DeletePartner" }
        }
    };
}
