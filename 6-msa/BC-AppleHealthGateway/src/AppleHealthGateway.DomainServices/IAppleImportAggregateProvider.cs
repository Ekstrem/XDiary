using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;

namespace AppleHealthGateway.DomainServices;

/// <summary>
/// РџСЂРѕРІР°Р№РґРµСЂ Р°РіСЂРµРіР°С‚Р° AppleHealthGateway.
/// </summary>
public interface IAppleImportAggregateProvider : IAggregateProvider<IAppleHealthGateway, IAppleImportAggregate> { }
