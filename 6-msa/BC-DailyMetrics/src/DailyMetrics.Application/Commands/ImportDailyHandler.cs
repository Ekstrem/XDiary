using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.DomainServices;
using MediatR;

namespace DailyMetrics.Application.Commands;

public class ImportDailyHandler : IRequestHandler<ImportDailyCommand, DailyMetricsOperationResult>
{
    private readonly IDailyMetricsAggregateProvider _provider;

    public ImportDailyHandler(IDailyMetricsAggregateProvider provider) => _provider = provider;

    public async Task<DailyMetricsOperationResult> Handle(ImportDailyCommand request, CancellationToken ct)
    {
        var draft = new DailyMetricsDraft
        {
            Date = request.Date,
            Stress = request.Stress,
            RestingHr = request.RestingHr,
            Hrv = request.Hrv,
            SleepScore = request.SleepScore,
            SleepDurationMin = request.SleepDurationMin,
            DeepMin = request.DeepMin,
            LightMin = request.LightMin,
            RemMin = request.RemMin,
            AwakeMin = request.AwakeMin
        };
        var aggregateId = DailyMetricsModelFactory.ToGuid(request.Date);

        var error = DailyMetricsModelFactory.Validate(draft);
        if (error != null)
            return DailyMetricsOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(IDailyMetricsAggregate.ImportDaily),
            "ImportDaily",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<IDailyMetrics>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(IDailyMetricsAggregate.ImportDaily), "ImportDaily",
            DailyMetricsModelFactory.BuildValueObjects(draft));

        var result = aggregate.ImportDaily(model, command);
        return DailyMetricsOperationResult.From(aggregateId, result);
    }
}
