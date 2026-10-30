using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class StartFromGarminHandler : IRequestHandler<StartFromGarminCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public StartFromGarminHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(StartFromGarminCommand request, CancellationToken ct)
    {
        var aggregateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.ToString("o");

        var vos = new Dictionary<string, IValueObject>
        {
            ["Root"] = RecordModelFactory.BuildRoot(Array.Empty<string>(), request.StartedAt),
            ["Duration"] = RecordModelFactory.BuildDuration(request.DurationMin),
            ["Origin"] = RecordModelFactory.BuildOrigin(true, request.GarminId, now, now)
        };

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);
        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.StartFromGarmin), "StartFromGarmin",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            aggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.StartFromGarmin), "StartFromGarmin", vos);

        var result = aggregate.StartFromGarmin(model, command);
        return JournalOperationResult.From(aggregateId, result);
    }
}
