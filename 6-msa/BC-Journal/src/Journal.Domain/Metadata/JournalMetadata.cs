using DigiTFactory.Libraries.AbstractAggregate.Metadata;
using DigiTFactory.Libraries.SeedWorks.Result;

namespace Journal.Domain.Metadata;

/// <summary>
/// Канонические метаданные агрегата Record (запись / акт).
/// 13 команд по экранам визарда (CJM) → по одному соразмерному событию.
/// Сущность = partners[] (0..N, 0 = соло) + startedAt.
/// </summary>
public static class JournalMetadata
{
    public static AggregateMetadata Build() => new()
    {
        Id = new Guid("a0000000-0000-0000-0000-000000000001"),
        AggregateName = "Record",
        Description = "Агрегат Record: фиксация акта. Сущность = partners[] + startedAt; визард → Recorded.",
        ValueObjects = new List<ValueObjectMetadata>
        {
            new()
            {
                Name = "Root", IsAggregateRoot = true, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Partners", TypeName = "string[]", IsRequired = false },
                    new() { Name = "StartedAt", TypeName = "string", IsRequired = false }
                }
            },
            new()
            {
                Name = "Duration", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "DurationMin", TypeName = "int", IsRequired = false } }
            },
            new()
            {
                Name = "Place", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Place", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Positions", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata> { new() { Name = "Positions", TypeName = "string[]", IsRequired = false } }
            },
            new()
            {
                Name = "Accessories", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Costumes", TypeName = "string[]", IsRequired = false },
                    new() { Name = "Toys", TypeName = "string[]", IsRequired = false },
                    new() { Name = "Fantasies", TypeName = "string[]", IsRequired = false },
                    new() { Name = "Tags", TypeName = "string[]", IsRequired = false }
                }
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
                Properties = new List<PropertyMetadata> { new() { Name = "Orgasms", TypeName = "string", IsRequired = false } }
            },
            new()
            {
                Name = "Origin", IsAggregateRoot = false, IsCollection = false,
                Properties = new List<PropertyMetadata>
                {
                    new() { Name = "Garmin", TypeName = "bool", IsRequired = false },
                    new() { Name = "GarminId", TypeName = "string", IsRequired = false },
                    new() { Name = "CreatedAt", TypeName = "string", IsRequired = false },
                    new() { Name = "UpdatedAt", TypeName = "string", IsRequired = false }
                }
            }
        },
        Operations = new List<OperationMetadata>
        {
            new()
            {
                CommandName = "StartManualEntry", Description = "Начать ручную запись.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Origin" },
                ProducedEvents = new[] { "ManualEntryStarted" },
                Invariants = new List<InvariantMetadata> { New() }
            },
            new()
            {
                CommandName = "StartFromGarmin", Description = "Начать запись из занятия Garmin.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root", "Duration", "Origin" },
                ProducedEvents = new[] { "GarminEntryStarted" },
                Invariants = new List<InvariantMetadata> { New() }
            },
            Step("SpecifyWhen", "WhenSpecified", "Root"),
            Step("AddPartner", "PartnerAdded", "Root"),
            Step("RemovePartner", "PartnerRemoved", "Root"),
            Step("SpecifyPlace", "PlaceSpecified", "Place"),
            Step("AddPositions", "PositionsAdded", "Positions"),
            Step("AddAccessories", "AccessoriesAdded", "Accessories"),
            Step("RecordContraception", "ContraceptionRecorded", "Contraception"),
            Step("SpecifyDuration", "DurationSpecified", "Duration"),
            Step("RecordOrgasms", "OrgasmsRecorded", "Orgasms"),
            new()
            {
                CommandName = "Complete", Description = "Завершить запись.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root" },
                ProducedEvents = new[] { "RecordCompleted" },
                Invariants = new List<InvariantMetadata>
                {
                    Existing(),
                    new() { Name = "WhenRequired", Type = InvariantType.Assertion, RuleExpression = "PropertyNotNull:Root.StartedAt", FailureReason = "Дата обязательна.", FailureResult = DomainOperationResultEnum.Exception }
                }
            },
            new()
            {
                CommandName = "Delete", Description = "Удалить запись.",
                MergeStrategy = OperationMergeStrategy.FullReplace,
                AffectedValueObjects = new[] { "Root" },
                ProducedEvents = new[] { "RecordDeleted" },
                Invariants = new List<InvariantMetadata> { Existing() }
            }
        },
        StateTransitions = new List<StateTransitionMetadata>
        {
            new() { FromState = "*", ToState = "Draft", TriggerOperation = "StartManualEntry" },
            new() { FromState = "*", ToState = "Draft", TriggerOperation = "StartFromGarmin" },
            new() { FromState = "Draft", ToState = "Draft", TriggerOperation = "SpecifyWhen" },
            new() { FromState = "Draft", ToState = "Draft", TriggerOperation = "AddPartner" },
            new() { FromState = "Draft", ToState = "Draft", TriggerOperation = "RemovePartner" },
            new() { FromState = "Draft", ToState = "Draft", TriggerOperation = "SpecifyPlace" },
            new() { FromState = "Draft", ToState = "Draft", TriggerOperation = "AddPositions" },
            new() { FromState = "Draft", ToState = "Draft", TriggerOperation = "AddAccessories" },
            new() { FromState = "Draft", ToState = "Draft", TriggerOperation = "RecordContraception" },
            new() { FromState = "Draft", ToState = "Draft", TriggerOperation = "SpecifyDuration" },
            new() { FromState = "Draft", ToState = "Draft", TriggerOperation = "RecordOrgasms" },
            new() { FromState = "Draft", ToState = "Recorded", TriggerOperation = "Complete" },
            new() { FromState = "Recorded", ToState = "Deleted", TriggerOperation = "Delete" }
        }
    };

    private static InvariantMetadata New()
        => new() { Name = "IsNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNewEntity", FailureReason = "Агрегат уже существует.", FailureResult = DomainOperationResultEnum.Exception };

    private static InvariantMetadata Existing()
        => new() { Name = "IsNotNewEntity", Type = InvariantType.Assertion, RuleExpression = "IsNotNewEntity", FailureReason = "Агрегат не существует.", FailureResult = DomainOperationResultEnum.Exception };

    private static OperationMetadata Step(string commandName, string eventName, string affectedVo)
        => new()
        {
            CommandName = commandName,
            Description = $"Шаг визарда: {commandName}.",
            MergeStrategy = OperationMergeStrategy.FullReplace,
            AffectedValueObjects = new[] { affectedVo },
            ProducedEvents = new[] { eventName },
            Invariants = new List<InvariantMetadata> { Existing() }
        };
}
