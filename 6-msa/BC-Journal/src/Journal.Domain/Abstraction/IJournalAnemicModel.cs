namespace Journal.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

public interface IJournalAnemicModel : IAnemicModel<IJournal>
{
    IRecordRoot Root { get; }
    int DurationMin { get; }
    string Place { get; }
    IReadOnlyList<string> Positions { get; }
    IReadOnlyList<string> Costumes { get; }
    IReadOnlyList<string> Toys { get; }
    IReadOnlyList<string> Fantasies { get; }
    IReadOnlyList<string> Tags { get; }
    IReadOnlyList<string> Contraceptives { get; }
    int ContraceptiveCount { get; }
    IReadOnlyList<IOrgasms> Orgasms { get; }
    bool Garmin { get; }
    string GarminId { get; }
    string CreatedAt { get; }
    string UpdatedAt { get; }
}
