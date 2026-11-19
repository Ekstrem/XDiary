namespace SomaticSeries.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ISomaticPointRoot : IAggregateRoot<ISomaticSeries> { string T { get; } }