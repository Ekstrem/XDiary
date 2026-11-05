namespace XDiary.Contracts;

using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>Convenience accessors on AggregateResult.</summary>
public static class AggregateResultExtensions
{
    public static bool IsSuccess<TBC, TModel>(this AggregateResult<TBC, TModel> result)
        where TBC : IBoundedContext
        where TModel : IAnemicModel<TBC>
        => result.Result == DomainOperationResultEnum.Success;

    public static TModel Model<TBC, TModel>(this AggregateResult<TBC, TModel> result)
        where TBC : IBoundedContext
        where TModel : IAnemicModel<TBC>
        => result.BusinessOperationData.Model;

    public static string? ErrorMessage<TBC, TModel>(this AggregateResult<TBC, TModel> result)
        where TBC : IBoundedContext
        where TModel : IAnemicModel<TBC>
        => result.Reason?.FirstOrDefault();
}
