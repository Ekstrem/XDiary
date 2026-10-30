using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class AddPartnerHandler : IRequestHandler<AddPartnerCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public AddPartnerHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(AddPartnerCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);
        var (partners, startedAt) = RecordModelFactory.ReadRoot(aggregate);

        var updated = new List<string>(partners);
        if (!updated.Contains(request.PartnerId))
            updated.Add(request.PartnerId);

        var vos = new Dictionary<string, IValueObject>
        {
            ["Root"] = RecordModelFactory.BuildRoot(updated, startedAt)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.AddPartner), "AddPartner",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.AddPartner), "AddPartner", vos);

        var result = aggregate.AddPartner(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
