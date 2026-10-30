namespace Portability.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ICsvDocumentAnemicModel : IAnemicModel<IPortability>
{
    ICsvDocumentRoot Root { get; }
    IReadOnlyList<string> Rows { get; }
}