namespace Privacy.Storage;
public sealed class PinCredentialEntity
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public bool Locked { get; set; }
    public long Version { get; set; }
}