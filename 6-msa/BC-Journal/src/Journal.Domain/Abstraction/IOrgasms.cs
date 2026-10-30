namespace Journal.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

public interface IOrgasms : IValueObject
{
    string Participant { get; }
    int Count { get; }
    IReadOnlyList<string> Types { get; }
    int Rating { get; }
}
