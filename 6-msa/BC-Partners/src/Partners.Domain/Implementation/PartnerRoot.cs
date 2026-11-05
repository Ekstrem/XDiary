namespace Partners.Domain.Implementation;
using System.Text.Json.Serialization;
using Partners.Domain.Abstraction;
public sealed class PartnerRoot : IPartnerRoot
{
    public string Name { get; private set; } = string.Empty;
    [JsonConstructor]
    private PartnerRoot(string name) { Name = name; }
    public static IPartnerRoot CreateInstance(string name) => new PartnerRoot(name);
}