using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;

namespace Journal.DomainServices;

/// <summary>
/// РџСЂРѕРІР°Р№РґРµСЂ Р°РіСЂРµРіР°С‚Р° Record.
/// </summary>
public interface IJournalAggregateProvider : IAggregateProvider<IJournal, IRecordAggregate> { }
