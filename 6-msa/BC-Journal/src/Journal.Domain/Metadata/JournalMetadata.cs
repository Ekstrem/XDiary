using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace Journal.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата Record (эквивалент seed_journal_metadata.sql).
/// Используются и в рантайме (in-memory кэш), и в доменных тестах —
/// источник истины один, расхождений нет.
/// </summary>
public static class JournalMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000001"),
        AggregateName = "Record",
        Description = "Агрегат Record: фиксация одного акта пары (дата, партнёр, место, позы, аксессуары, контрацепция, оргазмы, происхождение).",
        ValueObjects = new List<ValueObjectMetadata>
        {
            new()
            {
                Name = "Root", IsAggregateRoot = true, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "StartedAt", TypeName = "string", IsRequired = true },
                    new() { Name = "DurationMin", TypeName = "int", IsRequired = true }
                }
            },
            new()
            {
                Name = "Partner", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Name", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Place", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Name", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Positions", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Items", TypeName = "string[]", IsRequired = false } }
            },
            new()
            {
                Name = "Tags", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Items", TypeName = "string[]", IsRequired = false } }
            },
            new()
            {
                Name = "Costumes", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Items", TypeName = "string[]", IsRequired = false } }
            },
            new()
            {
                Name = "Toys", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Items", TypeName = "string[]", IsRequired = false } }
            },
            new()
            {
                Name = "Fantasies", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Items", TypeName = "string[]", IsRequired = false } }
            },
            new()
            {
                Name = "Contraception", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Contraceptives", TypeName = "string[]", IsRequired = false },
                    new() { Name = "ContraceptiveCount", TypeName = "int", IsRequired = false }
                }
            },
            new()
            {
                Name = "Orgasms", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "OrgasmsMe", TypeName = "int", IsRequired = false },
                    new() { Name = "OrgasmsPartner", TypeName = "int", IsRequired = false },
                    new() { Name = "OrgasmMeType", TypeName = "string[]", IsRequired = false },
                    new() { Name = "OrgasmPartnerType", TypeName = "string[]", IsRequired = false },
                    new() { Name = "OrgasmMeRating", TypeName = "int", IsRequired = false },
                    new() { Name = "OrgasmPartnerRating", TypeName = "int", IsRequired = false }
                }
            },
            new()
            {
                Name = "Origin", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Garmin", TypeName = "bool", IsRequired = false },
                    new() { Name = "GarminId", TypeName = "string", IsRequired = false }
                }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "CreateRecord",
                Description = "Создать новую запись из визарда.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Partner", "Place", "Positions", "Tags", "Costumes", "Toys", "Fantasies", "Contraception", "Orgasms", "Origin" },
                ProducedEvents = new[] { "RecordCreated" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustBeNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNewEntity", FailureReason = "Запись уже существует: создание невозможно.", FailureResult = DomainOperationResultEnum.Exception },
                    new() { Name = "StartedAtRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.StartedAt", FailureReason = "Дата/время начала обязательны.", FailureResult = DomainOperationResultEnum.Exception }
                }
            },
            new()
            {
                CommandName = "UpdateRecord",
                Description = "Отредактировать существующую запись.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Partner", "Place", "Positions", "Tags", "Costumes", "Toys", "Fantasies", "Contraception", "Orgasms", "Origin" },
                ProducedEvents = new[] { "RecordUpdated" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustNotBeNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNotNewEntity", FailureReason = "Запись не найдена для редактирования.", FailureResult = DomainOperationResultEnum.Exception },
                    new() { Name = "RootMustExist", Type = InvariantType.Assertion, RuleExpression = "VOExists:Root", FailureReason = "Корень записи не найден.", FailureResult = DomainOperationResultEnum.Exception }
                }
            },
            new()
            {
                CommandName = "DeleteRecord",
                Description = "Удалить запись.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = Array.Empty<string>(),
                ProducedEvents = new[] { "RecordDeleted" },
                Invariants = new List<InvariantMetadata>
                {
                    new() { Name = "MustNotBeNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNotNewEntity", FailureReason = "Запись не найдена для удаления.", FailureResult = DomainOperationResultEnum.Exception },
                    new() { Name = "RootMustExist", Type = InvariantType.Assertion, RuleExpression = "VOExists:Root", FailureReason = "Корень записи не найден.", FailureResult = DomainOperationResultEnum.Exception }
                }
            },
            new()
            {
                CommandName = "ImportRecord",
                Description = "Импортировать запись (upsert по id).",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Partner", "Place", "Positions", "Tags", "Costumes", "Toys", "Fantasies", "Contraception", "Orgasms", "Origin" },
                ProducedEvents = new[] { "RecordImported" },
                Invariants = new List<InvariantMetadata>()
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Created", TriggerOperation = "CreateRecord" },
            new() { FromState = "Created", ToState = "Created", TriggerOperation = "UpdateRecord" },
            new() { FromState = "Created", ToState = "Deleted", TriggerOperation = "DeleteRecord" },
            new() { FromState = "*", ToState = "Created", TriggerOperation = "ImportRecord" }
        }
    };
}
