using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class ChoosePartnerHandler : IRequestHandler<ChoosePartnerCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public ChoosePartnerHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(ChoosePartnerCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);
        var (_, startedAt) = RecordModelFactory.ReadRoot(aggregate);

        var vos = new Dictionary<string, IValueObject>
        {
            ["Root"] = RecordModelFactory.BuildRoot(request.PartnerId, startedAt)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.ChoosePartner), "ChoosePartner",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.ChoosePartner), "ChoosePartner", vos);

        var result = aggregate.ChoosePartner(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
