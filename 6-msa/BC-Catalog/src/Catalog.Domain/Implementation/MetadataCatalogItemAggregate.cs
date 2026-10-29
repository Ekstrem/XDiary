using System.Runtime.CompilerServices;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain.Abstraction;

namespace Catalog.Domain.Implementation;

/// <summary>
/// Metadata-driven реализация ICatalogItemAggregate.
/// Делегирует бизнес-операции в Aggregate&lt;ICatalog&gt;.Operations.
/// </summary>
public sealed class MetadataCatalogItemAggregate : ICatalogItemAggregate
{
    private readonly Aggregate<ICatalog> _aggregate;
    private readonly IAnemicModel<ICatalog> _currentModel;
    private readonly DynamicBoundedContextScope<ICatalog> _scope;

    public MetadataCatalogItemAggregate(
        Aggregate<ICatalog> aggregate,
        IAnemicModel<ICatalog> currentModel,
        DynamicBoundedContextScope<ICatalog> scope)
    {
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        _currentModel = currentModel ?? throw new ArgumentNullException(nameof(currentModel));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    #region IAggregate<ICatalog> — delegate to current model

    public Guid Id => _currentModel.Id;
    public long Version => _currentModel.Version;
    public string CommandName => _currentModel.CommandName;
    public string SubjectName => _currentModel.SubjectName;
    public Guid CorrelationToken => _currentModel.CorrelationToken;
    public IDictionary<string, IValueObject> Invariants => _currentModel.GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => _currentModel.GetValueObjects();

    #endregion

    #region Business operations

    public AggregateResult<ICatalog, IAnemicModel<ICatalog>> CreateItem(IAnemicModel<ICatalog> model)
        => ExecuteOperation(model, CreateCommand(model));

    public AggregateResult<ICatalog, IAnemicModel<ICatalog>> UpdateItem(IAnemicModel<ICatalog> model, ICommandToAggregate commandMetadata)
        => ExecuteOperation(model, ToCommandToAggregate(commandMetadata));

    public AggregateResult<ICatalog, IAnemicModel<ICatalog>> DeleteItem(IAnemicModel<ICatalog> model, ICommandToAggregate commandMetadata)
        => ExecuteOperation(model, ToCommandToAggregate(commandMetadata));

    #endregion

    #region Internal helpers

    private AggregateResult<ICatalog, IAnemicModel<ICatalog>> ExecuteOperation(
        IAnemicModel<ICatalog> incomingModel,
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

    private static CommandToAggregate CreateCommand(IAnemicModel<ICatalog> model)
        => CommandToAggregate.Commit(
            model.CorrelationToken,
            model.CommandName,
            model.SubjectName,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    private static CommandToAggregate ToCommandToAggregate(ICommandToAggregate cmd)
        => CommandToAggregate.Commit(
            cmd.CorrelationToken,
            cmd.CommandName,
            cmd.SubjectName,
            cmd.Version);

    #endregion

    public static MetadataCatalogItemAggregate Create(
        Aggregate<ICatalog> aggregate,
        IAnemicModel<ICatalog> currentModel,
        DynamicBoundedContextScope<ICatalog> scope)
        => new(aggregate, currentModel, scope);
}
