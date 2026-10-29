using System.Runtime.CompilerServices;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain.Abstraction;

namespace AppleHealthGateway.Domain.Implementation;

/// <summary>
/// Metadata-driven реализация IAppleImportAggregate.
/// Делегирует бизнес-операции в Aggregate&lt;IAppleHealthGateway&gt;.Operations.
/// </summary>
public sealed class MetadataAppleImportAggregate : IAppleImportAggregate
{
    private readonly Aggregate<IAppleHealthGateway> _aggregate;
    private readonly IAnemicModel<IAppleHealthGateway> _currentModel;
    private readonly DynamicBoundedContextScope<IAppleHealthGateway> _scope;

    public MetadataAppleImportAggregate(
        Aggregate<IAppleHealthGateway> aggregate,
        IAnemicModel<IAppleHealthGateway> currentModel,
        DynamicBoundedContextScope<IAppleHealthGateway> scope)
    {
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        _currentModel = currentModel ?? throw new ArgumentNullException(nameof(currentModel));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    #region IAggregate<IAppleHealthGateway> — delegate to current model

    public Guid Id => _currentModel.Id;
    public long Version => _currentModel.Version;
    public string CommandName => _currentModel.CommandName;
    public string SubjectName => _currentModel.SubjectName;
    public Guid CorrelationToken => _currentModel.CorrelationToken;
    public IDictionary<string, IValueObject> Invariants => _currentModel.GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => _currentModel.GetValueObjects();

    #endregion

    #region Business operations

    public AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>> UpsertImportState(IAnemicModel<IAppleHealthGateway> model, ICommandToAggregate commandMetadata)
        => ExecuteOperation(model, ToCommandToAggregate(commandMetadata));

    #endregion

    #region Internal helpers

    private AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>> ExecuteOperation(
        IAnemicModel<IAppleHealthGateway> incomingModel,
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

    public static MetadataAppleImportAggregate Create(
        Aggregate<IAppleHealthGateway> aggregate,
        IAnemicModel<IAppleHealthGateway> currentModel,
        DynamicBoundedContextScope<IAppleHealthGateway> scope)
        => new(aggregate, currentModel, scope);
}
