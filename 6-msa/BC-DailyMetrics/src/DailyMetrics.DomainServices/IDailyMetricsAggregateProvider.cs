using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;

namespace DailyMetrics.DomainServices;

/// <summary>
/// РџСЂРѕРІР°Р№РґРµСЂ Р°РіСЂРµРіР°С‚Р° DailyMetrics.
/// </summary>
public interface IDailyMetricsAggregateProvider : IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate> { }
