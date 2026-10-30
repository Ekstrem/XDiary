namespace AppleHealthGateway.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IImportConfig : IValueObject { IReadOnlyList<string> Metrics { get; } }