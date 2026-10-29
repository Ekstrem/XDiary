using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class DeleteRecordHandler : IRequestHandler<DeleteRecordCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public DeleteRecordHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(DeleteRecordCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IRecordAggregate.DeleteRecord),
            "DeleteRecord",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(),
            nameof(IRecordAggregate.DeleteRecord), "DeleteRecord",
            new Dictionary<string, IValueObject>());

        var result = aggregate.DeleteRecord(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
