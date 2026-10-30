namespace XDiary.Contracts;

using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>Command-side database context marker for persisting aggregate state.</summary>
public interface ICommandDbContext<TBC> where TBC : IBoundedContext
{
    Task SaveAsync(IAnemicModel<TBC> model, CancellationToken cancellationToken = default);
}

/// <summary>Read model marker.</summary>
public interface IReadModel<TBC> where TBC : IBoundedContext { }

/// <summary>Read repository.</summary>
public interface IReadRepository<TBC, TModel>
    where TBC : IBoundedContext
    where TModel : IReadModel<TBC>
{
    Task<TModel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TModel>> GetAllAsync(CancellationToken ct = default);
}

/// <summary>Command-side repository.</summary>
public interface IRepository<TBC, TModel>
    where TBC : IBoundedContext
    where TModel : IAnemicModel<TBC>
{
    Task<TModel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(AggregateResult<TBC, TModel> result, CancellationToken ct = default);
}

/// <summary>Bus adapter for a bounded context.</summary>
public interface IBusAdapter<TBC> where TBC : IBoundedContext
{
    Task Publish<TEvent>(TEvent @event, CancellationToken ct = default);
}

/// <summary>Notifier for aggregate result distribution.</summary>
public interface INotifier<TBC> where TBC : IBoundedContext
{
    void Notify<TModel>(AggregateResult<TBC, TModel> result)
        where TModel : IAnemicModel<TBC>;
}

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
