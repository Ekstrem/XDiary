namespace Portability.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ICsvDocumentRoot : IValueObject { IReadOnlyList<string> Headers { get; } }