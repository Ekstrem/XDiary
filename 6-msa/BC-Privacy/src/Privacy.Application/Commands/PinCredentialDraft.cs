namespace Privacy.Application.Commands;

/// <summary>
/// Черновик учётных данных PIN (PinCredential). Хранит только хеш и соль — не открытый PIN.
/// </summary>
public class PinCredentialDraft
{
    public string UserId { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public int Iterations { get; set; } = 100000;
}
