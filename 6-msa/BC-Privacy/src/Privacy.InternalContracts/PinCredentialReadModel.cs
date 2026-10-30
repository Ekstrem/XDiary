namespace Privacy.InternalContracts;
using XDiary.Contracts;
public sealed class PinCredentialReadModel : IReadModel<Privacy.Domain.IPrivacy>
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public bool Locked { get; set; }
    public bool HasPin => !string.IsNullOrEmpty(Hash);
}