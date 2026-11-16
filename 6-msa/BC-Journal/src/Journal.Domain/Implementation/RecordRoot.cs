namespace Journal.Domain.Implementation;

using Journal.Domain.Abstraction;

public sealed class RecordRoot : IRecordRoot
{
    public IReadOnlyList<string> Partners { get; private set; } = new List<string>();
    public DateTimeOffset StartedAt { get; private set; }

    private RecordRoot() { }

    public static IRecordRoot CreateInstance(IEnumerable<string> partners, DateTimeOffset startedAt)
        => new RecordRoot { Partners = partners.ToList(), StartedAt = startedAt };
}
