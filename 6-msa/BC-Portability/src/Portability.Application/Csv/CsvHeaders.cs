namespace Portability.Application.Csv;

/// <summary>
/// Фиксированный набор колонок CSV записей (Published Language).
/// Списки внутри полей — через `;` (позы, теги, игрушки…).
/// </summary>
public static class CsvHeaders
{
    public static readonly string[] Record = new[]
    {
        "id", "started_at", "duration_min", "partner", "place", "positions", "tags",
        "costumes", "toys", "fantasies", "contraceptives", "contraceptive_count",
        "orgasms_me", "orgasms_partner", "orgasm_me_type", "orgasm_me_rating",
        "orgasm_partner_type", "orgasm_partner_rating", "garmin"
    };
}
