namespace Journal.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

public interface IRecordRoot : IValueObject
{
    IReadOnlyList<string> Partners { get; }
    DateTimeOffset StartedAt { get; }
}
