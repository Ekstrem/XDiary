namespace Catalog.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ICatalogItemRoot : IValueObject { string Kind { get; } string Name { get; } }