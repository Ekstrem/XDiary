using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;

namespace Journal.Application.Commands;

/// <summary>
/// Строит Value Objects анемичной модели Record. Пошагово (визард) — только затронутые VO.
/// </summary>
internal static class RecordModelFactory
{
    public static IValueObject BuildRoot(string partnerId, string startedAt) => new DynamicValueObject("RecordRoot", new Dictionary<string, object?>
    {
        ["PartnerId"] = partnerId,
        ["StartedAt"] = startedAt
    });

    public static IValueObject BuildOrigin(bool garmin, string garminId, string createdAt, string updatedAt) => new DynamicValueObject("RecordOrigin", new Dictionary<string, object?>
    {
        ["Garmin"] = garmin,
        ["GarminId"] = garminId,
        ["CreatedAt"] = createdAt,
        ["UpdatedAt"] = updatedAt
    });

    public static IValueObject BuildDuration(int durationMin) => new DynamicValueObject("RecordDuration", new Dictionary<string, object?>
    {
        ["DurationMin"] = durationMin
    });

    public static IValueObject BuildPlace(string place) => new DynamicValueObject("RecordPlace", new Dictionary<string, object?>
    {
        ["Place"] = place
    });

    public static IValueObject BuildPositions(IEnumerable<string>? positions) => new DynamicValueObject("RecordPositions", new Dictionary<string, object?>
    {
        ["Positions"] = positions?.ToArray() ?? Array.Empty<string>()
    });

    public static IValueObject BuildAccessories(IEnumerable<string>? costumes, IEnumerable<string>? toys, IEnumerable<string>? fantasies, IEnumerable<string>? tags) => new DynamicValueObject("RecordAccessories", new Dictionary<string, object?>
    {
        ["Costumes"] = costumes?.ToArray() ?? Array.Empty<string>(),
        ["Toys"] = toys?.ToArray() ?? Array.Empty<string>(),
        ["Fantasies"] = fantasies?.ToArray() ?? Array.Empty<string>(),
        ["Tags"] = tags?.ToArray() ?? Array.Empty<string>()
    });

    public static IValueObject BuildContraception(IEnumerable<string>? contraceptives, int count) => new DynamicValueObject("RecordContraception", new Dictionary<string, object?>
    {
        ["Contraceptives"] = contraceptives?.ToArray() ?? Array.Empty<string>(),
        ["ContraceptiveCount"] = count
    });

    public static IValueObject BuildOrgasms(int me, int partner, IEnumerable<string>? meTypes, IEnumerable<string>? partnerTypes, int meRating, int partnerRating) => new DynamicValueObject("RecordOrgasms", new Dictionary<string, object?>
    {
        ["OrgasmsMe"] = me,
        ["OrgasmsPartner"] = partner,
        ["OrgasmMeType"] = meTypes?.ToArray() ?? Array.Empty<string>(),
        ["OrgasmPartnerType"] = partnerTypes?.ToArray() ?? Array.Empty<string>(),
        ["OrgasmMeRating"] = meRating,
        ["OrgasmPartnerRating"] = partnerRating
    });

    public static Dictionary<string, IValueObject> BuildValueObjects(RecordDraft d) => new()
    {
        ["Root"] = BuildRoot(d.PartnerId, d.StartedAt),
        ["Duration"] = BuildDuration(d.DurationMin),
        ["Place"] = BuildPlace(d.Place),
        ["Positions"] = BuildPositions(d.Positions),
        ["Accessories"] = BuildAccessories(d.Costumes, d.Toys, d.Fantasies, d.Tags),
        ["Contraception"] = BuildContraception(d.Contraceptives, d.ContraceptiveCount),
        ["Orgasms"] = BuildOrgasms(d.OrgasmsMe, d.OrgasmsPartner, d.OrgasmMeType, d.OrgasmPartnerType, d.OrgasmMeRating, d.OrgasmPartnerRating),
        ["Origin"] = BuildOrigin(d.Garmin, d.GarminId, d.CreatedAt, d.UpdatedAt)
    };

    /// <summary>Текущий корень (partnerId, startedAt) для слияния в шагах визарда.</summary>
    public static (string PartnerId, string StartedAt) ReadRoot(IRecordAggregate aggregate)
    {
        var vos = aggregate.GetValueObjects();
        if (vos.TryGetValue("Root", out var root) && root is DynamicValueObject r)
            return (r.Get<string>("PartnerId") ?? string.Empty, r.Get<string>("StartedAt") ?? string.Empty);
        return (string.Empty, string.Empty);
    }

    public static string? ValidateDuration(int durationMin)
        => durationMin < 1 ? "durationMin must be >= 1." : null;

    public static string? ValidateRating(int rating)
        => rating is < 0 or > 5 ? "rating must be 0..5." : null;
}
