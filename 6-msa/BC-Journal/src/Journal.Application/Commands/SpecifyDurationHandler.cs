using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class SpecifyDurationHandler : IRequestHandler<SpecifyDurationCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public SpecifyDurationHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(SpecifyDurationCommand request, CancellationToken ct)
    {
        var error = RecordModelFactory.ValidateDuration(request.DurationMin);
        if (error != null)
            return JournalOperationResult.Failed(request.AggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var vos = new Dictionary<string, IValueObject>
        {
            ["Duration"] = RecordModelFactory.BuildDuration(request.DurationMin)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.SpecifyDuration), "SpecifyDuration",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.SpecifyDuration), "SpecifyDuration", vos);

        var result = aggregate.SpecifyDuration(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
