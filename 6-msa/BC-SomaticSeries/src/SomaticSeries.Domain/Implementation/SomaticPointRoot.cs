namespace SomaticSeries.Domain.Implementation;
using SomaticSeries.Domain.Abstraction;
public sealed class SomaticPointRoot : ISomaticPointRoot
{
    public string T { get; private set; } = string.Empty;
    private SomaticPointRoot() { }
    public static ISomaticPointRoot CreateInstance(string t) => new SomaticPointRoot { T = t };
}