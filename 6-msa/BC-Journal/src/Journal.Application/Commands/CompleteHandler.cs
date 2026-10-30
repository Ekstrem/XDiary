using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class CompleteHandler : IRequestHandler<CompleteCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public CompleteHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(CompleteCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);
        var (partners, startedAt) = RecordModelFactory.ReadRoot(aggregate);

        if (string.IsNullOrWhiteSpace(startedAt))
            return JournalOperationResult.Failed(request.AggregateId, "startedAt is required before Complete.");

        var vos = new Dictionary<string, IValueObject>
        {
            ["Root"] = RecordModelFactory.BuildRoot(partners, startedAt)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.Complete), "Complete",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.Complete), "Complete", vos);

        var result = aggregate.Complete(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
