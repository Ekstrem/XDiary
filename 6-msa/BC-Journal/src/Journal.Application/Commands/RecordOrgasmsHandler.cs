using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class RecordOrgasmsHandler : IRequestHandler<RecordOrgasmsCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public RecordOrgasmsHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(RecordOrgasmsCommand request, CancellationToken ct)
    {
        foreach (var orgasm in request.Orgasms)
        {
            var error = RecordModelFactory.ValidateRating(orgasm.Rating);
            if (error != null)
                return JournalOperationResult.Failed(request.AggregateId, error);
        }

        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var vos = new Dictionary<string, IValueObject>
        {
            ["Orgasms"] = RecordModelFactory.BuildOrgasms(request.Orgasms)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.RecordOrgasms), "RecordOrgasms",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.RecordOrgasms), "RecordOrgasms", vos);

        var result = aggregate.RecordOrgasms(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
