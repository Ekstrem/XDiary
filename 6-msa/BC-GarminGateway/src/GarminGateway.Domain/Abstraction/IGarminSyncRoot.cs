namespace GarminGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IGarminSyncRoot : IValueObject { Guid ClientId { get; } string SourceId { get; } }