namespace AppleHealthGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IAppleImportRoot : IValueObject { string SourceId { get; } }