namespace Journal.Domain.Implementation;

using Journal.Domain.Abstraction;

// QG015/QG006: VO РёРјРјСѓС‚Р°Р±РµР»РµРЅ вЂ” getter-only СЃРІРѕР№СЃС‚РІР° + РїСЂРёРІР°С‚РЅС‹Р№ РєРѕРЅСЃС‚СЂСѓРєС‚РѕСЂ + С„Р°Р±СЂРёРєР°.
internal sealed class Orgasms : IOrgasms
{
    public string Participant { get; }
    public int Count { get; }
    public IReadOnlyList<string> Types { get; }
    public int Rating { get; }

    private Orgasms(string participant, int count, IReadOnlyList<string> types, int rating)
    {
        Participant = participant;
        Count = count;
        Types = types;
        Rating = rating;
    }

    public static IOrgasms CreateInstance(string participant, int count, IEnumerable<string> types, int rating)
        => new Orgasms(participant, count, types.ToList(), rating);
}
