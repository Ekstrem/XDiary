using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;

namespace SomaticSeries.DomainServices;

/// <summary>
/// РџСЂРѕРІР°Р№РґРµСЂ Р°РіСЂРµРіР°С‚Р° SomaticPoint.
/// </summary>
public interface ISomaticSeriesAggregateProvider : IAggregateProvider<ISomaticSeries, ISomaticPointAggregate> { }
