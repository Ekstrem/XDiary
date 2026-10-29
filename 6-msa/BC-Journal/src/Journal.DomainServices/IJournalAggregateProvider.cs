using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;

namespace Journal.DomainServices;

/// <summary>
/// Провайдер агрегата Record.
/// </summary>
public interface IJournalAggregateProvider : IAggregateProvider<IJournal, IRecordAggregate> { }
