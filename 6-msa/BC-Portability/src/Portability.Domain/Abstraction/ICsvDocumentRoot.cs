namespace Portability.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ICsvDocumentRoot : IAggregateRoot<IPortability> { IReadOnlyList<string> Headers { get; } }