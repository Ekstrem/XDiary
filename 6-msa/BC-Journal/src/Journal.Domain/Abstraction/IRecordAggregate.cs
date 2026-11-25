namespace Journal.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;

/// <summary>
/// Интерфейс агрегата записи журнала (канон EDM): агрегат — это состояние (анемичная модель)
/// + 13 бизнес-операций. Живёт в Abstraction/ — реализация в Implementation/.
/// </summary>
public interface IRecordAggregate : IAggregate<IJournal>, IJournalAnemicModel
{
    AggregateResult<IJournal, IJournalAnemicModel> StartManualEntry(Guid clientId, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> StartFromGarmin(string startedAt, int durationMin, string garminId, Guid clientId, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> SpecifyWhen(string startedAt, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> AddPartner(string partnerId, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> RemovePartner(string partnerId, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> SpecifyPlace(string place, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> AddPositions(IEnumerable<string> positions, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> AddAccessories(IEnumerable<string> costumes, IEnumerable<string> toys, IEnumerable<string> fantasies, IEnumerable<string> tags, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> RecordContraception(IEnumerable<string> contraceptives, int count, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> SpecifyDuration(int durationMin, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> RecordOrgasms(IEnumerable<IOrgasms> orgasms, CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> Complete(CommandToAggregate command);
    AggregateResult<IJournal, IJournalAnemicModel> Delete(CommandToAggregate command);
}
