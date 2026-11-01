namespace AppleHealthGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IAppleImportRoot : IValueObject { Guid ClientId { get; } string SourceId { get; } }