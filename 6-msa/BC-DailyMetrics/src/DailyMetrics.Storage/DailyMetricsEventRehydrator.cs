namespace DailyMetrics.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;
using XDiary.Contracts;

/// <summary>
/// BE-149: доменный порт Full Event Sourcing (CommandRepository.Postgres 0.3.0) —
/// сворачивает сохранённые события в последовательность состояний AnemicModel
/// (тот же fold, что DailyMetricsEventReplay).
/// </summary>
public sealed class DailyMetricsEventRehydrator : IEventStreamRehydrator<IDailyMetrics, IDailyMetricsAnemicModel>
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IValueObjectJsonConverter());
        return options;
    }

    public IReadOnlyList<IDailyMetricsAnemicModel> Rehydrate(IReadOnlyList<DomainEventEntry> events)
    {
        if (events.Count == 0)
        {
            return Array.Empty<IDailyMetricsAnemicModel>();
        }

        var states = new List<IDailyMetricsAnemicModel>(events.Count);
        var model = new AnemicModel { Id = events[0].Id };
        foreach (var e in events.OrderBy(e => e.Version))
        {
            var vos = JsonSerializer.Deserialize<IDictionary<string, IValueObject>>(e.ChangedValueObjectsJson, Json);
            if (vos is null || vos.Count == 0)
            {
                continue;
            }

            model = DailyMetricsEventReplay.Apply(model, vos, e);
            states.Add(model);
        }

        return states;
    }
}