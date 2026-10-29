using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;

namespace Portability.DomainServices;

/// <summary>
/// РџСЂРѕРІР°Р№РґРµСЂ Р°РіСЂРµРіР°С‚Р° Portability.
/// </summary>
public interface ICsvDocumentAggregateProvider : IAggregateProvider<IPortability, ICsvDocumentAggregate> { }
