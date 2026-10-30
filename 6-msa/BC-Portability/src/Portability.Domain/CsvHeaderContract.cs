namespace Portability.Domain;
public static class CsvHeaderContract
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "id","started_at","duration_min","partner","place","positions","tags","costumes","toys","fantasies","contraceptives","contraceptive_count","orgasms_me","orgasms_partner","orgasm_me_type","orgasm_me_rating","orgasm_partner_type","orgasm_partner_rating","garmin"
    };
}