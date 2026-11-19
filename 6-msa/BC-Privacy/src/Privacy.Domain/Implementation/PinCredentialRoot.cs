namespace Privacy.Domain.Implementation;
using Privacy.Domain.Abstraction;

// QG015/QG011: VO иммутабелен — positional record (getter-only) + фабрика CreateInstance.
internal sealed record PinCredentialRoot(Guid ClientId, string Salt, string Hash, int Iterations) : IPinCredentialRoot
{
    public static IPinCredentialRoot CreateInstance(Guid clientId, string salt, string hash, int iterations) => new PinCredentialRoot(clientId, salt, hash, iterations);
}
