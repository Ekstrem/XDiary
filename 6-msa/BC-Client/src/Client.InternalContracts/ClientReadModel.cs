namespace Client.InternalContracts;
using XDiary.Contracts;
public sealed class ClientReadModel : IReadModel<Client.Domain.IClients>
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public bool Verified { get; set; }
}