namespace Journal.InternalContracts;

using XDiary.Contracts;

public sealed class JournalReadModel : IReadModel<Journal.Domain.IJournal>
{
    public Guid Id { get; set; }
    public IReadOnlyList<string> Partners { get; set; } = new List<string>();
    public string StartedAt { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public string Place { get; set; } = string.Empty;
    public IReadOnlyList<string> Positions { get; set; } = new List<string>();
    public IReadOnlyList<string> Tags { get; set; } = new List<string>();
    public IReadOnlyList<string> Costumes { get; set; } = new List<string>();
    public IReadOnlyList<string> Toys { get; set; } = new List<string>();
    public IReadOnlyList<string> Fantasies { get; set; } = new List<string>();
    public IReadOnlyList<string> Contraceptives { get; set; } = new List<string>();
    public int ContraceptiveCount { get; set; }
    public IReadOnlyList<OrgasmsReadModel> Orgasms { get; set; } = new List<OrgasmsReadModel>();
    public bool Garmin { get; set; }
    public string GarminId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

public sealed class OrgasmsReadModel
{
    public string Participant { get; set; } = "me";
    public int Count { get; set; }
    public IReadOnlyList<string> Types { get; set; } = new List<string>();
    public int Rating { get; set; }
}
