using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;

namespace GarminGateway.DomainServices;

/// <summary>
/// РџСЂРѕРІР°Р№РґРµСЂ Р°РіСЂРµРіР°С‚Р° GarminGateway.
/// </summary>
public interface IGarminSyncAggregateProvider : IAggregateProvider<IGarminGateway, IGarminSyncAggregate> { }
