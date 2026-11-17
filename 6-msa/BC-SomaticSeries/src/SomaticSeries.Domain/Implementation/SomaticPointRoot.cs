namespace SomaticSeries.Domain.Implementation;
using SomaticSeries.Domain.Abstraction;

// QG015/QG011: VO иммутабелен — positional record (getter-only) + фабрика CreateInstance.
public sealed record SomaticPointRoot(string T) : ISomaticPointRoot
{
    public static ISomaticPointRoot CreateInstance(string t) => new SomaticPointRoot(t);
}
