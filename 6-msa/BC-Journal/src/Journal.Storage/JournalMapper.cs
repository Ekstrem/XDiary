namespace Journal.Storage;
using System.Text.Json;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Journal.InternalContracts;
public static class JournalMapper
{
    public static string ToJson(IEnumerable<string>? items) => JsonSerializer.Serialize(items?.ToList() ?? new List<string>());
    public static List<string> ToList(string? json) => string.IsNullOrWhiteSpace(json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    public static string OrgasmsToJson(IEnumerable<IOrgasms> orgasms) => JsonSerializer.Serialize(orgasms.Select(o => new OrgasmsReadModel { Participant = o.Participant, Count = o.Count, Types = o.Types.ToList(), Rating = o.Rating }).ToList());
    public static List<OrgasmsReadModel> OrgasmsToList(string? json) => string.IsNullOrWhiteSpace(json) ? new List<OrgasmsReadModel>() : JsonSerializer.Deserialize<List<OrgasmsReadModel>>(json) ?? new List<OrgasmsReadModel>();

    public static IJournalAnemicModel MapToModel(RecordEntity e)
    {
        return new AnemicModel
        {
            Id = e.Id,
            Version = e.Version,
            Root = RecordRoot.CreateInstance(ToList(e.PartnersJson), e.StartedAt),
            DurationMin = e.DurationMin,
            Place = e.Place,
            Positions = ToList(e.PositionsJson),
            Tags = ToList(e.TagsJson),
            Costumes = ToList(e.CostumesJson),
            Toys = ToList(e.ToysJson),
            Fantasies = ToList(e.FantasiesJson),
            Contraceptives = ToList(e.ContraceptivesJson),
            ContraceptiveCount = e.ContraceptiveCount,
            Orgasms = OrgasmsToList(e.OrgasmsJson).Select(o => Orgasms.CreateInstance(o.Participant, o.Count, o.Types, o.Rating)).ToList(),
            ClientId = e.ClientId,
            Garmin = e.Garmin,
            GarminId = e.GarminId,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            Deleted = e.Deleted,
            State = (RecordState)e.State,
        };
    }

    public static void MapToEntity(IJournalAnemicModel m, RecordEntity e)
    {
        e.Id = m.Id; e.Version = m.Version; e.ClientId = m.ClientId; e.StartedAt = m.Root.StartedAt;
        e.PartnersJson = ToJson(m.Root.Partners); e.DurationMin = m.DurationMin; e.Place = m.Place;
        e.PositionsJson = ToJson(m.Positions); e.TagsJson = ToJson(m.Tags); e.CostumesJson = ToJson(m.Costumes);
        e.ToysJson = ToJson(m.Toys); e.FantasiesJson = ToJson(m.Fantasies); e.ContraceptivesJson = ToJson(m.Contraceptives);
        e.ContraceptiveCount = m.ContraceptiveCount; e.OrgasmsJson = OrgasmsToJson(m.Orgasms);
        e.Garmin = m.Garmin; e.GarminId = m.GarminId; e.CreatedAt = m.CreatedAt; e.UpdatedAt = m.UpdatedAt; e.Deleted = m.Deleted; e.State = (int)m.State;
    }

    public static JournalReadModel MapToReadModel(RecordEntity e) => new JournalReadModel
    {
        Id = e.Id, ClientId = e.ClientId, Partners = ToList(e.PartnersJson), StartedAt = e.StartedAt, DurationMin = e.DurationMin,
        Place = e.Place, Positions = ToList(e.PositionsJson), Tags = ToList(e.TagsJson), Costumes = ToList(e.CostumesJson),
        Toys = ToList(e.ToysJson), Fantasies = ToList(e.FantasiesJson), Contraceptives = ToList(e.ContraceptivesJson),
        ContraceptiveCount = e.ContraceptiveCount, Orgasms = OrgasmsToList(e.OrgasmsJson), Garmin = e.Garmin,
        GarminId = e.GarminId, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt, Deleted = e.Deleted, State = e.State
    };
}