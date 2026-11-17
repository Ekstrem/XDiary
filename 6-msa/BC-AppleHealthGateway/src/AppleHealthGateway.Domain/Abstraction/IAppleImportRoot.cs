namespace AppleHealthGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IAppleImportRoot : IAggregateRoot<IAppleHealthGateway> { Guid ClientId { get; } string SourceId { get; } }