using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Journal.Domain.Abstraction;

/// <summary>
/// Агрегат Record (запись/акт) контекста журнала.
/// Методы делегируют в Aggregate&lt;IJournal&gt;.Operations из AbstractAggregate.
/// </summary>
public interface IRecordAggregate : IAggregate<IJournal>
{
    /// <summary>Создать новую запись из визарда.</summary>
    AggregateResult<IJournal, IAnemicModel<IJournal>> CreateRecord(IAnemicModel<IJournal> model);

    /// <summary>Отредактировать существующую запись.</summary>
    AggregateResult<IJournal, IAnemicModel<IJournal>> UpdateRecord(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);

    /// <summary>Удалить запись.</summary>
    AggregateResult<IJournal, IAnemicModel<IJournal>> DeleteRecord(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);

    /// <summary>Импортировать запись (upsert по id).</summary>
    AggregateResult<IJournal, IAnemicModel<IJournal>> ImportRecord(IAnemicModel<IJournal> model, ICommandToAggregate commandMetadata);
}
