using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class UpdateRecordHandler : IRequestHandler<UpdateRecordCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public UpdateRecordHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(UpdateRecordCommand request, CancellationToken ct)
    {
        var draft = request.Draft ?? new RecordDraft();

        var error = RecordModelFactory.Validate(draft);
        if (error != null)
            return JournalOperationResult.Failed(request.AggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IRecordAggregate.UpdateRecord),
            "UpdateRecord",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(),
            nameof(IRecordAggregate.UpdateRecord), "UpdateRecord",
            RecordModelFactory.BuildValueObjects(draft));

        var result = aggregate.UpdateRecord(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
