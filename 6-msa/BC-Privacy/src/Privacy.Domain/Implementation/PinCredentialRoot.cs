namespace Privacy.Domain.Implementation;
using Privacy.Domain.Abstraction;
public sealed class PinCredentialRoot : IPinCredentialRoot
{
    public string Salt { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;
    public int Iterations { get; private set; }
    private PinCredentialRoot() { }
    public static IPinCredentialRoot CreateInstance(string salt, string hash, int iterations) => new PinCredentialRoot { Salt = salt, Hash = hash, Iterations = iterations };
}