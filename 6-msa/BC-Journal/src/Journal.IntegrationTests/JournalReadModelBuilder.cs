using Journal.Domain.Abstraction;
using Journal.InternalContracts;

namespace Journal.IntegrationTests;

/// <summary>
/// Прямой маппинг модели агрегата в read-модель (замена удалённого JournalMapper/RecordEntity —
/// легаси-контур EF удалён, маппинг для тестов остался прямым model → JournalReadModel).
/// </summary>
public static class JournalReadModelBuilder
{
    public static JournalReadModel ToReadModel(IJournalAnemicModel m) => new JournalReadModel
    {
        Id = m.Id,
        Version = m.Version,
        ClientId = m.ClientId,
        Partners = m.Root.Partners,
        StartedAt = m.Root.StartedAt.ToString("O"),
        DurationMin = m.DurationMin,
        Place = m.Place,
        Positions = m.Positions,
        Tags = m.Tags,
        Costumes = m.Costumes,
        Toys = m.Toys,
        Fantasies = m.Fantasies,
        Contraceptives = m.Contraceptives,
        ContraceptiveCount = m.ContraceptiveCount,
        Orgasms = m.Orgasms.Select(o => new OrgasmsReadModel { Participant = o.Participant, Count = o.Count, Types = o.Types, Rating = o.Rating }).ToList(),
        Garmin = m.Garmin,
        GarminId = m.GarminId,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        Deleted = m.Deleted,
        State = (int)m.State,
    };
}
