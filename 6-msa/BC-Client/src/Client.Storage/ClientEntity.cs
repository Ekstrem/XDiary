namespace Client.Storage;
public sealed class ClientEntity
{
    public Guid Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
    public DateTime? OtpExpiresAt { get; set; }
    public int OtpAttempts { get; set; }
    public bool Verified { get; set; }
    public Guid? LinkedPartnerId { get; set; }
    public long Version { get; set; }
}