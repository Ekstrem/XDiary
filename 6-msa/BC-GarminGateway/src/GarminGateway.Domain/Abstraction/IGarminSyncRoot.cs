namespace GarminGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IGarminSyncRoot : IAggregateRoot<IGarminGateway> { Guid ClientId { get; } string SourceId { get; } }