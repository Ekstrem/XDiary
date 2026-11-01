namespace Client.Domain.Implementation;
using Client.Domain.Abstraction;
public sealed class ClientRoot : IClientRoot
{
    public string Phone { get; private set; } = string.Empty;
    private ClientRoot() { }
    public static IClientRoot CreateInstance(string phone) => new ClientRoot { Phone = phone };
}