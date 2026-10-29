using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class ImportRecordHandler : IRequestHandler<ImportRecordCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public ImportRecordHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(ImportRecordCommand request, CancellationToken ct)
    {
        var draft = request.Draft ?? new RecordDraft();
        var aggregateId = request.AggregateId == Guid.Empty ? Guid.NewGuid() : request.AggregateId;

        var error = RecordModelFactory.Validate(draft);
        if (error != null)
            return JournalOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IRecordAggregate.ImportRecord),
            "ImportRecord",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IJournal>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(IRecordAggregate.ImportRecord), "ImportRecord",
            RecordModelFactory.BuildValueObjects(draft));

        var result = aggregate.ImportRecord(model, command);
        return JournalOperationResult.From(aggregateId, result);
    }
}
