namespace GarminGateway.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using XDiary.Contracts;

/// <summary>
/// BE-149: доменный порт Full Event Sourcing (CommandRepository.Postgres 0.3.0) —
/// сворачивает сохранённые события в последовательность состояний AnemicModel
/// (тот же fold, что GarminGatewayEventReplay).
/// </summary>
public sealed class GarminGatewayEventRehydrator : IEventStreamRehydrator<IGarminGateway, IGarminGatewayAnemicModel>
{
    private static readonly IValueObjectSerializer Serializer = ValueObjectSerializerFactory.CreateDefault();

    public IReadOnlyList<IGarminGatewayAnemicModel> Rehydrate(IReadOnlyList<DomainEventEntry> events)
    {
        if (events.Count == 0)
        {
            return Array.Empty<IGarminGatewayAnemicModel>();
        }

        var states = new List<IGarminGatewayAnemicModel>(events.Count);
        var model = new AnemicModel { Id = events[0].Id };
        foreach (var e in events.OrderBy(e => e.Version))
        {
            var vos = Serializer.DeserializeWithLegacy(e.ChangedValueObjectsJson);
            if (vos is null || vos.Count == 0)
            {
                continue;
            }

            model = GarminGatewayEventReplay.Apply(model, vos, e);
            states.Add(model);
        }

        return states;
    }
}