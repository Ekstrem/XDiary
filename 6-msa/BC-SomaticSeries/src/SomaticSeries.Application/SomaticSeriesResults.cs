namespace SomaticSeries.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using XDiary.Contracts;
public static class SomaticSeriesResults
{
    public static AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> Fail(string error)
    { var d = BusinessOperationData<ISomaticSeries, ISomaticSeriesAnemicModel>.Commit<ISomaticSeries, ISomaticSeriesAnemicModel>(default!, default!); return new AggregateResultException<ISomaticSeries, ISomaticSeriesAnemicModel>(d, new FailedSpecification<ISomaticSeries, ISomaticSeriesAnemicModel>(error)); }
}