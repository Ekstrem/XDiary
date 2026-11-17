namespace Journal.Domain.Implementation;

using Journal.Domain.Abstraction;

// record (positional) вЂ” С‡С‚РѕР±С‹ event-sourced replay РјРѕРі РґРµСЃРµСЂРёР°Р»РёР·РѕРІР°С‚СЊ Root РёР· ChangedValueObjectsJson.
internal sealed record RecordRoot(IReadOnlyList<string> Partners, DateTimeOffset StartedAt) : IRecordRoot
{
    public static IRecordRoot CreateInstance(IEnumerable<string> partners, DateTimeOffset startedAt)
        => new RecordRoot(partners.ToList(), startedAt);
}
