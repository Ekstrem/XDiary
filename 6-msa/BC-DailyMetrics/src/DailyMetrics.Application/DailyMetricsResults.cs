namespace DailyMetrics.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using XDiary.Contracts;
public static class DailyMetricsResults
{
    public static AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> Fail(string error)
    { var d = BusinessOperationData<IDailyMetrics, IDailyMetricsAnemicModel>.Commit<IDailyMetrics, IDailyMetricsAnemicModel>(default!, default!); return new AggregateResultException<IDailyMetrics, IDailyMetricsAnemicModel>(d, new FailedSpecification<IDailyMetrics, IDailyMetricsAnemicModel>(error)); }
}