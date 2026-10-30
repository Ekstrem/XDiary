using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using MediatR;

namespace Journal.Application.Commands;

public class RecordContraceptionHandler : IRequestHandler<RecordContraceptionCommand, JournalOperationResult>
{
    private readonly IJournalAggregateProvider _provider;

    public RecordContraceptionHandler(IJournalAggregateProvider provider) => _provider = provider;

    public async Task<JournalOperationResult> Handle(RecordContraceptionCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var vos = new Dictionary<string, IValueObject>
        {
            ["Contraception"] = RecordModelFactory.BuildContraception(request.Contraceptives, request.ContraceptiveCount)
        };

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(), nameof(IRecordAggregate.RecordContraception), "RecordContraception",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var model = DynamicAnemicModel<IJournal>.Create(
            request.AggregateId, 0, Guid.NewGuid(), nameof(IRecordAggregate.RecordContraception), "RecordContraception", vos);

        var result = aggregate.RecordContraception(model, command);
        return JournalOperationResult.From(request.AggregateId, result);
    }
}
