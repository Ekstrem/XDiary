using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class RemovePartnerHandler : IRequestHandler<RemovePartnerCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public RemovePartnerHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(RemovePartnerCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);
        var (partners, startedAt) = RecordModelFactory.ReadRoot(aggregate);

        var updated = partners.Where(p => p != request.PartnerId).ToList();

        var vos = new Dictionary<string, IValueObject>
        {
            ["Root"] = RecordModelFactory.BuildRoot(updated, startedAt)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.RemovePartner), "RemovePartner",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.RemovePartner), "RemovePartner", vos);

        var result = aggregate.RemovePartner(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
