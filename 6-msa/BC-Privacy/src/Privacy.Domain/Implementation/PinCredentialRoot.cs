namespace Privacy.Domain.Implementation;
using Privacy.Domain.Abstraction;
public sealed class PinCredentialRoot : IPinCredentialRoot
{
    public Guid ClientId { get; private set; }
    public string Salt { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;
    public int Iterations { get; private set; }
    private PinCredentialRoot() { }
    public static IPinCredentialRoot CreateInstance(Guid clientId, string salt, string hash, int iterations) => new PinCredentialRoot { ClientId = clientId, Salt = salt, Hash = hash, Iterations = iterations };
}