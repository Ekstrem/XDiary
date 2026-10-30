namespace GarminGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IGarminSyncRoot : IValueObject { string SourceId { get; } }