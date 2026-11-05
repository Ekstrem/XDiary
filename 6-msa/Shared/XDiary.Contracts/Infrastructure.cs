namespace XDiary.Contracts;

using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>Command-side repository.</summary>
public interface IRepository<TBC, TModel>
    where TBC : IBoundedContext
    where TModel : IAnemicModel<TBC>
{
    Task<TModel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(AggregateResult<TBC, TModel> result, CancellationToken ct = default);
}
