using System.Runtime.CompilerServices;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain.Abstraction;

namespace Privacy.Domain.Implementation;

/// <summary>
/// Metadata-driven реализация IPinCredentialAggregate.
/// Делегирует бизнес-операции в Aggregate&lt;IPrivacy&gt;.Operations.
/// </summary>
public sealed class MetadataPinCredentialAggregate : IPinCredentialAggregate
{
    private readonly Aggregate<IPrivacy> _aggregate;
    private readonly IAnemicModel<IPrivacy> _currentModel;
    private readonly DynamicBoundedContextScope<IPrivacy> _scope;

    public MetadataPinCredentialAggregate(
        Aggregate<IPrivacy> aggregate,
        IAnemicModel<IPrivacy> currentModel,
        DynamicBoundedContextScope<IPrivacy> scope)
    {
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        _currentModel = currentModel ?? throw new ArgumentNullException(nameof(currentModel));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    #region IAggregate<IPrivacy> — delegate to current model

    public Guid Id => _currentModel.Id;
    public long Version => _currentModel.Version;
    public string CommandName => _currentModel.CommandName;
    public string SubjectName => _currentModel.SubjectName;
    public Guid CorrelationToken => _currentModel.CorrelationToken;
    public IDictionary<string, IValueObject> Invariants => _currentModel.GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => _currentModel.GetValueObjects();

    #endregion

    #region Business operations

    public AggregateResult<IPrivacy, IAnemicModel<IPrivacy>> SetPin(IAnemicModel<IPrivacy> model, ICommandToAggregate commandMetadata)
        => ExecuteOperation(model, ToCommandToAggregate(commandMetadata));

    public AggregateResult<IPrivacy, IAnemicModel<IPrivacy>> RemovePin(IAnemicModel<IPrivacy> model, ICommandToAggregate commandMetadata)
        => ExecuteOperation(model, ToCommandToAggregate(commandMetadata));

    #endregion

    #region Internal helpers

    private AggregateResult<IPrivacy, IAnemicModel<IPrivacy>> ExecuteOperation(
        IAnemicModel<IPrivacy> incomingModel,
        CommandToAggregate command,
        [CallerMemberName] string operationName = "")
    {
        if (!_aggregate.Operations.TryGetValue(operationName, out var operation))
            throw new InvalidOperationException(
                $"Operation '{operationName}' not found in aggregate metadata. " +
                $"Available operations: {string.Join(", ", _aggregate.Operations.Keys)}");

        _scope.CurrentModel = _currentModel;

        return operation.Handle(incomingModel, command, _scope);
    }

    private static CommandToAggregate ToCommandToAggregate(ICommandToAggregate cmd)
        => CommandToAggregate.Commit(
            cmd.CorrelationToken,
            cmd.CommandName,
            cmd.SubjectName,
            cmd.Version);

    #endregion

    public static MetadataPinCredentialAggregate Create(
        Aggregate<IPrivacy> aggregate,
        IAnemicModel<IPrivacy> currentModel,
        DynamicBoundedContextScope<IPrivacy> scope)
        => new(aggregate, currentModel, scope);
}
