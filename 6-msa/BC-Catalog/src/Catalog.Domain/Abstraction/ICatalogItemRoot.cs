namespace Catalog.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface ICatalogItemRoot : IAggregateRoot<ICatalog> { string Kind { get; } string Name { get; } }