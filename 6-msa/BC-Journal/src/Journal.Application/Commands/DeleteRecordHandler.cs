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

        var vos = new Dictionary<string, IValueObject>();
        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.Delete), "Delete",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.Delete), "Delete", vos);

        var result = aggregate.Delete(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
