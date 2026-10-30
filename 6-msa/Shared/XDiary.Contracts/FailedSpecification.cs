namespace XDiary.Contracts;

using DigiTFactory.Libraries.SeedWorks.Definition;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// Спецификация, которая всегда не выполняется с заданной причиной.
/// Используется для конструирования AggregateResultException.
/// </summary>
public sealed class FailedSpecification<TBC, TModel> : IBusinessOperationSpecification<TBC, TModel>
    where TBC : IBoundedContext
    where TModel : IAnemicModel<TBC>
{
    public FailedSpecification(string reason) => Reason = reason;

    public bool IsSatisfiedBy(BusinessOperationData<TBC, TModel> obj) => false;

    public string Reason { get; }

    public DomainOperationResultEnum DomainResult => DomainOperationResultEnum.Exception;
}
