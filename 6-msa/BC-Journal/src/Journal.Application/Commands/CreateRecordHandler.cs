using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class CreateRecordHandler : IRequestHandler<CreateRecordCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public CreateRecordHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(CreateRecordCommand request, CancellationToken ct)
    {
        var draft = request.Draft ?? new RecordDraft();
        var aggregateId = Guid.NewGuid();

        var error = RecordModelFactory.Validate(draft);
        if (error != null)
            return JournalOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);
        var model = DynamicAnemicModel<IJournal>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(IRecordAggregate.CreateRecord), "CreateRecord",
            RecordModelFactory.BuildValueObjects(draft));

        var result = aggregate.CreateRecord(model);
        return JournalOperationResult.From(aggregateId, result);
    }
}
