namespace Journal.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;

/// <summary>
/// Интерфейс агрегата записи журнала (канон EDM): агрегат — это состояние (анемичная модель)
/// + 13 бизнес-операций. Живёт в Abstraction/ — реализация в Implementation/.
/// </summary>
public interface IRecordAggregate : IAggregate<IJournal>, IJournalAnemicModel
{
    AggregateResult<IJournal, IJournalAnemicModel> StartManualEntry(Guid clientId);
    AggregateResult<IJournal, IJournalAnemicModel> StartFromGarmin(string startedAt, int durationMin, string garminId, Guid clientId);
    AggregateResult<IJournal, IJournalAnemicModel> SpecifyWhen(string startedAt);
    AggregateResult<IJournal, IJournalAnemicModel> AddPartner(string partnerId);
    AggregateResult<IJournal, IJournalAnemicModel> RemovePartner(string partnerId);
    AggregateResult<IJournal, IJournalAnemicModel> SpecifyPlace(string place);
    AggregateResult<IJournal, IJournalAnemicModel> AddPositions(IEnumerable<string> positions);
    AggregateResult<IJournal, IJournalAnemicModel> AddAccessories(IEnumerable<string> costumes, IEnumerable<string> toys, IEnumerable<string> fantasies, IEnumerable<string> tags);
    AggregateResult<IJournal, IJournalAnemicModel> RecordContraception(IEnumerable<string> contraceptives, int count);
    AggregateResult<IJournal, IJournalAnemicModel> SpecifyDuration(int durationMin);
    AggregateResult<IJournal, IJournalAnemicModel> RecordOrgasms(IEnumerable<IOrgasms> orgasms);
    AggregateResult<IJournal, IJournalAnemicModel> Complete();
    AggregateResult<IJournal, IJournalAnemicModel> Delete();
}
