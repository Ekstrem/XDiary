using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.DomainServices;
using MediatR;

namespace SomaticSeries.Application.Commands;

public class ImportPointHandler : IRequestHandler<ImportPointCommand, SomaticSeriesOperationResult>
{
    private readonly ISomaticSeriesAggregateProvider _provider;

    public ImportPointHandler(ISomaticSeriesAggregateProvider provider) => _provider = provider;

    public async Task<SomaticSeriesOperationResult> Handle(ImportPointCommand request, CancellationToken ct)
    {
        var draft = new SomaticPointDraft { T = request.T, Hr = request.Hr, BodyBattery = request.BodyBattery };
        var aggregateId = SomaticPointModelFactory.ToGuid(request.T);

        var error = SomaticPointModelFactory.Validate(draft);
        if (error != null)
            return SomaticSeriesOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(ISomaticPointAggregate.ImportPoint),
            "ImportPoint",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<ISomaticSeries>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(ISomaticPointAggregate.ImportPoint), "ImportPoint",
            SomaticPointModelFactory.BuildValueObjects(draft));

        var result = aggregate.ImportPoint(model, command);
        return SomaticSeriesOperationResult.From(aggregateId, result);
    }
}
