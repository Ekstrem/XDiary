namespace Journal.Domain.Implementation;

using Journal.Domain.Abstraction;

public sealed class Orgasms : IOrgasms
{
    public string Participant { get; private set; } = "me";
    public int Count { get; private set; }
    public IReadOnlyList<string> Types { get; private set; } = new List<string>();
    public int Rating { get; private set; }

    private Orgasms() { }

    public static IOrgasms CreateInstance(string participant, int count, IEnumerable<string> types, int rating)
        => new Orgasms { Participant = participant, Count = count, Types = types.ToList(), Rating = rating };
}
