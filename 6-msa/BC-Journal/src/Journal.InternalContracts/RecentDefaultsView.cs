namespace Journal.InternalContracts;
using XDiary.Contracts;
public sealed class PartnerRef
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
public sealed class RecentDefaultsView : IReadModel<Journal.Domain.IJournal>
{
    public PartnerRef? LastPartner { get; set; }
    public string? LastPlace { get; set; }
    public IReadOnlyList<string> LastPositions { get; set; } = new List<string>();
    public IReadOnlyList<string> LastTags { get; set; } = new List<string>();
    public IReadOnlyList<string> LastContraceptives { get; set; } = new List<string>();
    public IReadOnlyList<string> LastFantasies { get; set; } = new List<string>();
}