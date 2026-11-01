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
}