namespace DailyMetrics.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Specifications;
using XDiary.Contracts;

public sealed class Aggregate
{
    public IDailyMetricsAnemicModel Model { get; }
    private Aggregate(IDailyMetricsAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IDailyMetricsAnemicModel model) => new(model);

    private AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<IDailyMetrics, IDailyMetricsAnemicModel>.Commit<IDailyMetrics, IDailyMetricsAnemicModel>(Model, m); return new AggregateResultSuccess<IDailyMetrics, IDailyMetricsAnemicModel>(d); }
    private AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IDailyMetrics, IDailyMetricsAnemicModel>.Commit<IDailyMetrics, IDailyMetricsAnemicModel>(Model, Model); return new AggregateResultException<IDailyMetrics, IDailyMetricsAnemicModel>(d, new FailedSpecification<IDailyMetrics, IDailyMetricsAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Stress = Model.Stress, RestingHr = Model.RestingHr, Hrv = Model.Hrv, Sleep = Model.Sleep };

    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordStress(string date, int stress)
    {
        var dv = new DateFormatValidator(); if (!dv.IsSatisfiedBy(date)) return Fail(dv.Reason);
        var nv = new NonNegativeValidator(); if (!nv.IsSatisfiedBy(stress)) return Fail(nv.Reason);
        var m = Clone(); m.Root = DailyMetricsRoot.CreateInstance(date); m.Stress = stress; return Success(m);
    }
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordRestingHr(string date, int restingHr)
    {
        var dv = new DateFormatValidator(); if (!dv.IsSatisfiedBy(date)) return Fail(dv.Reason);
        var nv = new NonNegativeValidator(); if (!nv.IsSatisfiedBy(restingHr)) return Fail(nv.Reason);
        var m = Clone(); m.Root = DailyMetricsRoot.CreateInstance(date); m.RestingHr = restingHr; return Success(m);
    }
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordHrv(string date, int hrv)
    {
        var dv = new DateFormatValidator(); if (!dv.IsSatisfiedBy(date)) return Fail(dv.Reason);
        var nv = new NonNegativeValidator(); if (!nv.IsSatisfiedBy(hrv)) return Fail(nv.Reason);
        var m = Clone(); m.Root = DailyMetricsRoot.CreateInstance(date); m.Hrv = hrv; return Success(m);
    }
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordSleep(string date, ISleep sleep)
    {
        var dv = new DateFormatValidator(); if (!dv.IsSatisfiedBy(date)) return Fail(dv.Reason);
        var m = Clone(); m.Root = DailyMetricsRoot.CreateInstance(date); m.Sleep = sleep; return Success(m);
    }
}