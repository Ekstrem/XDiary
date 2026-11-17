namespace Journal.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

public interface IRecordRoot : IAggregateRoot<IJournal>
{
    IReadOnlyList<string> Partners { get; }
    DateTimeOffset StartedAt { get; }
}
