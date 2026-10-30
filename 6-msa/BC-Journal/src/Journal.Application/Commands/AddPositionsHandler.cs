using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class AddPositionsHandler : IRequestHandler<AddPositionsCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public AddPositionsHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(AddPositionsCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var vos = new Dictionary<string, IValueObject>
        {
            ["Positions"] = RecordModelFactory.BuildPositions(request.Positions)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.AddPositions), "AddPositions",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.AddPositions), "AddPositions", vos);

        var result = aggregate.AddPositions(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
