namespace Portability.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;
using XDiary.Contracts;

/// <summary>
/// BE-149: доменный порт Full Event Sourcing (CommandRepository.Postgres 0.3.0) —
/// сворачивает сохранённые события в последовательность состояний AnemicModel
/// (тот же fold, что PortabilityEventReplay).
/// </summary>
public sealed class PortabilityEventRehydrator : IEventStreamRehydrator<IPortability, ICsvDocumentAnemicModel>
{
    private static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new IValueObjectJsonConverter());
        return options;
    }

    public IReadOnlyList<ICsvDocumentAnemicModel> Rehydrate(IReadOnlyList<DomainEventEntry> events)
    {
        if (events.Count == 0)
        {
            return Array.Empty<ICsvDocumentAnemicModel>();
        }

        var states = new List<ICsvDocumentAnemicModel>(events.Count);
        var model = new AnemicModel { Id = events[0].Id };
        foreach (var e in events.OrderBy(e => e.Version))
        {
            var vos = JsonSerializer.Deserialize<IDictionary<string, IValueObject>>(e.ChangedValueObjectsJson, Json);
            if (vos is null || vos.Count == 0)
            {
                continue;
            }

            model = PortabilityEventReplay.Apply(model, vos, e);
            states.Add(model);
        }

        return states;
    }
}