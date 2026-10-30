using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class SpecifyWhenHandler : IRequestHandler<SpecifyWhenCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public SpecifyWhenHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(SpecifyWhenCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);
        var (partners, _) = RecordModelFactory.ReadRoot(aggregate);

        var vos = new Dictionary<string, IValueObject>
        {
            ["Root"] = RecordModelFactory.BuildRoot(partners, request.StartedAt)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.SpecifyWhen), "SpecifyWhen",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.SpecifyWhen), "SpecifyWhen", vos);

        var result = aggregate.SpecifyWhen(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
