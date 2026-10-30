using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class SpecifyPlaceHandler : IRequestHandler<SpecifyPlaceCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public SpecifyPlaceHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(SpecifyPlaceCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var vos = new Dictionary<string, IValueObject>
        {
            ["Place"] = RecordModelFactory.BuildPlace(request.Place)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.SpecifyPlace), "SpecifyPlace",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.SpecifyPlace), "SpecifyPlace", vos);

        var result = aggregate.SpecifyPlace(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
