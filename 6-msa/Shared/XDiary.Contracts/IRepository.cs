namespace XDiary.Contracts;

using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>Command-side repository.</summary>
/// <remarks>BE-056 (E3): последний самописный интерфейс. Заменяется каноном DF:
/// GetByIdAsync → IAnemicModelRepository.GetById; SaveAsync → IEventBusProducer.Publish(result.Event).
/// Держится отдельным файлом, пока раскатка E1 не уберёт его по всем BC.</remarks>
public interface IRepository<TBC, TModel>
    where TBC : IBoundedContext
    where TModel : IAnemicModel<TBC>
{
    Task<TModel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(AggregateResult<TBC, TModel> result, CancellationToken ct = default);
}
