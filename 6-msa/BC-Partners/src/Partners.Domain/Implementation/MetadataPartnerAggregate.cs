using System.Runtime.CompilerServices;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain.Abstraction;

namespace Partners.Domain.Implementation;

/// <summary>
/// Metadata-driven реализация IPartnerAggregate.
/// Делегирует бизнес-операции в Aggregate&lt;IPartners&gt;.Operations.
/// </summary>
public sealed class MetadataPartnerAggregate : IPartnerAggregate
{
    private readonly Aggregate<IPartners> _aggregate;
    private readonly IAnemicModel<IPartners> _currentModel;
    private readonly DynamicBoundedContextScope<IPartners> _scope;

    public MetadataPartnerAggregate(
        Aggregate<IPartners> aggregate,
        IAnemicModel<IPartners> currentModel,
        DynamicBoundedContextScope<IPartners> scope)
    {
        _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        _currentModel = currentModel ?? throw new ArgumentNullException(nameof(currentModel));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    #region IAggregate<IPartners> — delegate to current model

    public Guid Id => _currentModel.Id;
    public long Version => _currentModel.Version;
    public string CommandName => _currentModel.CommandName;
    public string SubjectName => _currentModel.SubjectName;
    public Guid CorrelationToken => _currentModel.CorrelationToken;
    public IDictionary<string, IValueObject> Invariants => _currentModel.GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => _currentModel.GetValueObjects();

    #endregion

    #region Business operations

    public AggregateResult<IPartners, IAnemicModel<IPartners>> CreatePartner(IAnemicModel<IPartners> model)
        => ExecuteOperation(model, CreateCommand(model));

    public AggregateResult<IPartners, IAnemicModel<IPartners>> UpdatePartner(IAnemicModel<IPartners> model, ICommandToAggregate commandMetadata)
        => ExecuteOperation(model, ToCommandToAggregate(commandMetadata));

    public AggregateResult<IPartners, IAnemicModel<IPartners>> DeletePartner(IAnemicModel<IPartners> model, ICommandToAggregate commandMetadata)
        => ExecuteOperation(model, ToCommandToAggregate(commandMetadata));

    #endregion

    #region Internal helpers

    private AggregateResult<IPartners, IAnemicModel<IPartners>> ExecuteOperation(
        IAnemicModel<IPartners> incomingModel,
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

    private static CommandToAggregate CreateCommand(IAnemicModel<IPartners> model)
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

    public static MetadataPartnerAggregate Create(
        Aggregate<IPartners> aggregate,
        IAnemicModel<IPartners> currentModel,
        DynamicBoundedContextScope<IPartners> scope)
        => new(aggregate, currentModel, scope);
}
