namespace AppleHealthGateway.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;
using XDiary.Contracts;

/// <summary>
/// BE-149: доменный порт Full Event Sourcing (CommandRepository.Postgres 0.3.0) —
/// сворачивает сохранённые события в последовательность состояний AnemicModel
/// (тот же fold, что AppleHealthGatewayEventReplay).
/// </summary>
public sealed class AppleHealthGatewayEventRehydrator : IEventStreamRehydrator<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>
{
    private static readonly IValueObjectSerializer Serializer = ValueObjectSerializerFactory.CreateDefault();

    public IReadOnlyList<IAppleHealthGatewayAnemicModel> Rehydrate(IReadOnlyList<DomainEventEntry> events)
    {
        if (events.Count == 0)
        {
            return Array.Empty<IAppleHealthGatewayAnemicModel>();
        }

        var states = new List<IAppleHealthGatewayAnemicModel>(events.Count);
        var model = new AnemicModel { Id = events[0].Id };
        foreach (var e in events.OrderBy(e => e.Version))
        {
            var vos = Serializer.DeserializeWithLegacy(e.ChangedValueObjectsJson);
            if (vos is null || vos.Count == 0)
            {
                continue;
            }

            model = AppleHealthGatewayEventReplay.Apply(model, vos, e);
            states.Add(model);
        }

        return states;
    }
}