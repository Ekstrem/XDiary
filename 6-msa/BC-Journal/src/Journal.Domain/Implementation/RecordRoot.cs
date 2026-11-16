namespace Journal.Domain.Implementation;

using Journal.Domain.Abstraction;

// record (positional) — чтобы event-sourced replay мог десериализовать Root из ChangedValueObjectsJson.
public sealed record RecordRoot(IReadOnlyList<string> Partners, DateTimeOffset StartedAt) : IRecordRoot
{
    public static IRecordRoot CreateInstance(IEnumerable<string> partners, DateTimeOffset startedAt)
        => new RecordRoot(partners.ToList(), startedAt);
}
