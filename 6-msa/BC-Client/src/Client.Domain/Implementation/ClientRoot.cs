namespace Client.Domain.Implementation;
using Client.Domain.Abstraction;
// Канон EDM: VO иммутабелен — positional record (getter-only), фабрика CreateInstance.
internal sealed record ClientRoot(string Phone) : IClientRoot
{
    public static IClientRoot CreateInstance(string phone) => new ClientRoot(phone);
}
