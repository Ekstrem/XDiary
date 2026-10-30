using System.Runtime.CompilerServices;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain.Abstraction;

namespace Journal.Domain.Implementation;

/// <summary>
/// Metadata-driven реализация IRecordAggregate.
/// Делегирует бизнес-операции в Aggregate&lt;IJournal&gt;.Operations.
/// </summary>
public sealed class MetadataRecordAggregate : IRecordAggregate
{
    private readonly Aggregate<IJournal> _aggregate;
    private readonly IAnemicModel<IJournal> _currentModel;
    private readonly DynamicBoundedContextScope<IJournal> _scope;

    public MetadataRecordAggregate(
        Aggregate<IJournal> aggregate,
        IAnemicModel<IJournal> currentModel,
        DynamicBoundedContextScope<IJournal> scope)
    {
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        _currentModel = currentModel ?? throw new ArgumentNullException(nameof(currentModel));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    #region IAggregate<IJournal> — delegate to current model

    public Guid Id => _currentModel.Id;
    public long Version => _currentModel.Version;
    public string CommandName => _currentModel.CommandName;
    public string SubjectName => _currentModel.SubjectName;
    public Guid CorrelationToken => _currentModel.CorrelationToken;
    public IDictionary<string, IValueObject> Invariants => _currentModel.GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => _currentModel.GetValueObjects();

    #endregion

    #region Business operations

    public AggregateResult<IJournal, IAnemicModel<IJournal>> StartManualEntry(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> StartFromGarmin(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> SpecifyWhen(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> ChoosePartner(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> SpecifyPlace(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> AddPositions(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> AddAccessories(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> RecordContraception(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> SpecifyDuration(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> RecordOrgasms(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> Complete(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);
    public AggregateResult<IJournal, IAnemicModel<IJournal>> Delete(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata)
        => Execute(model, commandMetadata);

    #endregion

    #region Internal helpers

    private AggregateResult<IJournal, IAnemicModel<IJournal>> Execute(
        IAnemicModel<IJournal> incomingModel,
        ICommandToAggregate commandMetadata,
        [CallerMemberName] string operationName = "")
    {
        if (!_aggregate.Operations.TryGetValue(operationName, out var operation))
            throw new InvalidOperationException(
                $"Operation '{operationName}' not found in aggregate metadata. " +
                $"Available operations: {string.Join(", ", _aggregate.Operations.Keys)}");

        _scope.CurrentModel = _currentModel;

        return operation.Handle(incomingModel, ToCommandToAggregate(commandMetadata), _scope);
    }

    private static CommandToAggregate ToCommandToAggregate(ICommandToAggregate cmd)
        => CommandToAggregate.Commit(cmd.CorrelationToken, cmd.CommandName, cmd.SubjectName, cmd.Version);

    #endregion

    public static MetadataRecordAggregate Create(
        Aggregate<IJournal> aggregate,
        IAnemicModel<IJournal> currentModel,
        DynamicBoundedContextScope<IJournal> scope)
        => new(aggregate, currentModel, scope);
}
