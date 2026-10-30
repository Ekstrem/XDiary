namespace Partners.Domain.Implementation;
using Partners.Domain.Abstraction;
public sealed class PartnerRoot : IPartnerRoot
{
    public string Name { get; private set; } = string.Empty;
    private PartnerRoot() { }
    public static IPartnerRoot CreateInstance(string name) => new PartnerRoot { Name = name };
}