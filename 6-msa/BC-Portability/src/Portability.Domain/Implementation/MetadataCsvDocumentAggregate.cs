using System.Runtime.CompilerServices;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain.Abstraction;

namespace Portability.Domain.Implementation;

/// <summary>
/// Metadata-driven реализация ICsvDocumentAggregate.
/// Делегирует бизнес-операции в Aggregate&lt;IPortability&gt;.Operations.
/// </summary>
public sealed class MetadataCsvDocumentAggregate : ICsvDocumentAggregate
{
    private readonly Aggregate<IPortability> _aggregate;
    private readonly IAnemicModel<IPortability> _currentModel;
    private readonly DynamicBoundedContextScope<IPortability> _scope;

    public MetadataCsvDocumentAggregate(
        Aggregate<IPortability> aggregate,
        IAnemicModel<IPortability> currentModel,
        DynamicBoundedContextScope<IPortability> scope)
    {
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        _currentModel = currentModel ?? throw new ArgumentNullException(nameof(currentModel));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    #region IAggregate<IPortability> — delegate to current model

    public Guid Id => _currentModel.Id;
    public long Version => _currentModel.Version;
    public string CommandName => _currentModel.CommandName;
    public string SubjectName => _currentModel.SubjectName;
    public Guid CorrelationToken => _currentModel.CorrelationToken;
    public IDictionary<string, IValueObject> Invariants => _currentModel.GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => _currentModel.GetValueObjects();

    #endregion

    #region Business operations

    public AggregateResult<IPortability, IAnemicModel<IPortability>> UpsertDocument(IAnemicModel<IPortability> model, ICommandToAggregate commandMetadata)
        => ExecuteOperation(model, ToCommandToAggregate(commandMetadata));

    #endregion

    #region Internal helpers

    private AggregateResult<IPortability, IAnemicModel<IPortability>> ExecuteOperation(
        IAnemicModel<IPortability> incomingModel,
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

    public static MetadataCsvDocumentAggregate Create(
        Aggregate<IPortability> aggregate,
        IAnemicModel<IPortability> currentModel,
        DynamicBoundedContextScope<IPortability> scope)
        => new(aggregate, currentModel, scope);
}
