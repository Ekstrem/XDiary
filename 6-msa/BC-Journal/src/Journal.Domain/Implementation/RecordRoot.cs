namespace Journal.Domain.Implementation;

using Journal.Domain.Abstraction;

public sealed class RecordRoot : IRecordRoot
{
    public IReadOnlyList<string> Partners { get; private set; } = new List<string>();
    public string StartedAt { get; private set; } = string.Empty;

    private RecordRoot() { }

    public static IRecordRoot CreateInstance(IEnumerable<string> partners, string startedAt)
        => new RecordRoot { Partners = partners.ToList(), StartedAt = startedAt };
}
