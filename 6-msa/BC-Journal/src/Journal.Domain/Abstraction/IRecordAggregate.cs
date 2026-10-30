using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Journal.Domain.Abstraction;

/// <summary>
/// Агрегат Record: 12 команд по экранам визарда.
/// </summary>
public interface IRecordAggregate : IAggregate<IJournal>
{
    AggregateResult<IJournal, IAnemicModel<IJournal>> StartManualEntry(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> StartFromGarmin(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> SpecifyWhen(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> ChoosePartner(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> SpecifyPlace(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> AddPositions(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> AddAccessories(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> RecordContraception(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> SpecifyDuration(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> RecordOrgasms(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> Complete(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
    AggregateResult<IJournal, IAnemicModel<IJournal>> Delete(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
}
