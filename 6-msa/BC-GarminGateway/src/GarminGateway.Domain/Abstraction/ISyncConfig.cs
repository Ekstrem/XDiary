namespace GarminGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ISyncConfig : IValueObject { IReadOnlyList<string> Metrics { get; } int WindowDays { get; } }