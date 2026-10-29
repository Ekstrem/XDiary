using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace Catalog.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата CatalogItem.
/// Используются и в рантайме (in-memory кэш), и в доменных тестах.
/// </summary>
public static class CatalogMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000003"),
        AggregateName = "CatalogItem",
        Description = "Агрегат CatalogItem: элемент каталога (костюм/игрушка/фантазия/контрацепция).",
        ValueObjects = new List<ValueObjectMetadata>
        {
            new()
            {
                Name = "Root", IsAggregateRoot = true, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Kind", TypeName = "string", IsRequired = true },
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
                Name = "Notes", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Text", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Brand", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Value", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Archive", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Archived", TypeName = "bool", IsRequired = false } }
            },
            new()
            {
                Name = "FantasyPartner", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Partner", TypeName = "string", IsRequired = false } }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "CreateItem",
                Description = "Создать элемент каталога.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Photo", "Notes", "Brand", "Archive", "FantasyPartner" },
                ProducedEvents = new[] { "ItemCreated" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustBeNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNewEntity", FailureReason = "Элемент уже существует: создание невозможно.", FailureResult = DomainOperationResultEnum.Exception },
                    new() { Name = "NameRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.Name", FailureReason = "Имя элемента обязательно.", FailureResult = DomainOperationResultEnum.Exception }
                }
            },
            new()
            {
                CommandName = "UpdateItem",
                Description = "Отредактировать элемент каталога.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Photo", "Notes", "Brand", "Archive", "FantasyPartner" },
                ProducedEvents = new[] { "ItemUpdated" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustNotBeNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNotNewEntity", FailureReason = "Элемент не найден для редактирования.", FailureResult = DomainOperationResultEnum.Exception },
                    new() { Name = "RootMustExist", Type = InvariantType.Assertion, RuleExpression = "VOExists:Root", FailureReason = "Корень элемента не найден.", FailureResult = DomainOperationResultEnum.Exception }
                }
            },
            new()
            {
                CommandName = "DeleteItem",
                Description = "Удалить элемент каталога.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = Array.Empty<string>(),
                ProducedEvents = new[] { "ItemDeleted" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustNotBeNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNotNewEntity", FailureReason = "Элемент не найден для удаления.", FailureResult = DomainOperationResultEnum.Exception },
                    new() { Name = "RootMustExist", Type = InvariantType.Assertion, RuleExpression = "VOExists:Root", FailureReason = "Корень элемента не найден.", FailureResult = DomainOperationResultEnum.Exception }
                }
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Active", TriggerOperation = "CreateItem" },
            new() { FromState = "Active", ToState = "Active", TriggerOperation = "UpdateItem" },
            new() { FromState = "Active", ToState = "Deleted", TriggerOperation = "DeleteItem" }
        }
    };
}
