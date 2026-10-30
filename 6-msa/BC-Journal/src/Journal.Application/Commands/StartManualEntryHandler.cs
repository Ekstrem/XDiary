using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class StartManualEntryHandler : IRequestHandler<StartManualEntryCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public StartManualEntryHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(StartManualEntryCommand request, CancellationToken ct)
    {
        var aggregateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.ToString("o");

        var vos = new Dictionary<string, IValueObject>
        {
            ["Root"] = RecordModelFactory.BuildRoot(Array.Empty<string>(), string.Empty),
            ["Origin"] = RecordModelFactory.BuildOrigin(false, string.Empty, now, now)
        };

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);
        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.StartManualEntry), "StartManualEntry",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            aggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.StartManualEntry), "StartManualEntry", vos);

        var result = aggregate.StartManualEntry(model, command);
        return JournalOperationResult.From(aggregateId, result);
    }
}
