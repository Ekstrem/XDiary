namespace Client.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using XDiary.Contracts;

/// <summary>
/// BE-149: доменный порт Full Event Sourcing (CommandRepository.Postgres 0.3.0) —
/// сворачивает сохранённые события в последовательность состояний AnemicModel
/// (тот же fold, что ClientEventReplay).
/// </summary>
public sealed class ClientEventRehydrator : IEventStreamRehydrator<IClients, IClientAnemicModel>
{
    private static readonly IValueObjectSerializer Serializer = ValueObjectSerializerProvider.Create();

    public IReadOnlyList<IClientAnemicModel> Rehydrate(IReadOnlyList<DomainEventEntry> events)
    {
        if (events.Count == 0)
        {
            return Array.Empty<IClientAnemicModel>();
        }

        var states = new List<IClientAnemicModel>(events.Count);
        var model = new AnemicModel { Id = events[0].Id };
        foreach (var e in events.OrderBy(e => e.Version))
        {
            var vos = ValueObjectSerializerProvider.DeserializeWithLegacy(Serializer, e.ChangedValueObjectsJson);
            if (vos is null || vos.Count == 0)
            {
                continue;
            }

            model = ClientEventReplay.Apply(model, vos, e);
            states.Add(model);
        }

        return states;
    }
}