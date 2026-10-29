using System.Runtime.CompilerServices;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain.Abstraction;

namespace SomaticSeries.Domain.Implementation;

/// <summary>
/// Metadata-driven реализация ISomaticPointAggregate.
/// Делегирует бизнес-операции в Aggregate&lt;ISomaticSeries&gt;.Operations.
/// </summary>
public sealed class MetadataSomaticPointAggregate : ISomaticPointAggregate
{
    private readonly Aggregate<ISomaticSeries> _aggregate;
    private readonly IAnemicModel<ISomaticSeries> _currentModel;
    private readonly DynamicBoundedContextScope<ISomaticSeries> _scope;

    public MetadataSomaticPointAggregate(
        Aggregate<ISomaticSeries> aggregate,
        IAnemicModel<ISomaticSeries> currentModel,
        DynamicBoundedContextScope<ISomaticSeries> scope)
    {
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        _currentModel = currentModel ?? throw new ArgumentNullException(nameof(currentModel));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    #region IAggregate<ISomaticSeries> — delegate to current model

    public Guid Id => _currentModel.Id;
    public long Version => _currentModel.Version;
    public string CommandName => _currentModel.CommandName;
    public string SubjectName => _currentModel.SubjectName;
    public Guid CorrelationToken => _currentModel.CorrelationToken;
    public IDictionary<string, IValueObject> Invariants => _currentModel.GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => _currentModel.GetValueObjects();

    #endregion

    #region Business operations

    public AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>> ImportPoint(IAnemicModel<ISomaticSeries> model, ICommandToAggregate commandMetadata)
        => ExecuteOperation(model, ToCommandToAggregate(commandMetadata));

    #endregion

    #region Internal helpers

    private AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>> ExecuteOperation(
        IAnemicModel<ISomaticSeries> incomingModel,
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

    public static MetadataSomaticPointAggregate Create(
        Aggregate<ISomaticSeries> aggregate,
        IAnemicModel<ISomaticSeries> currentModel,
        DynamicBoundedContextScope<ISomaticSeries> scope)
        => new(aggregate, currentModel, scope);
}
