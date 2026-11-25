namespace Partners.Storage;

using System.Text.Json;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using XDiary.Contracts;

/// <summary>
/// BE-149: доменный порт Full Event Sourcing (CommandRepository.Postgres 0.3.0) —
/// сворачивает сохранённые события в последовательность состояний AnemicModel
/// (тот же fold, что PartnersEventReplay).
/// </summary>
public sealed class PartnersEventRehydrator : IEventStreamRehydrator<IPartners, IPartnerAnemicModel>
{
    private static readonly IValueObjectSerializer Serializer = ValueObjectSerializerProvider.Create();

    public IReadOnlyList<IPartnerAnemicModel> Rehydrate(IReadOnlyList<DomainEventEntry> events)
    {
        if (events.Count == 0)
        {
            return Array.Empty<IPartnerAnemicModel>();
        }

        var states = new List<IPartnerAnemicModel>(events.Count);
        var model = new AnemicModel { Id = events[0].Id };
        foreach (var e in events.OrderBy(e => e.Version))
        {
            var vos = ValueObjectSerializerProvider.DeserializeWithLegacy(Serializer, e.ChangedValueObjectsJson);
            if (vos is null || vos.Count == 0)
            {
                continue;
            }

            model = PartnersEventReplay.Apply(model, vos, e);
            states.Add(model);
        }

        return states;
    }
}