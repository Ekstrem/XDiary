namespace Client.InternalContracts;
using XDiary.Contracts;
public sealed class ClientReadModel : IReadModel<Client.Domain.IClients>
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool Verified { get; set; }
}