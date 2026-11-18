namespace DailyMetrics.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Specifications;
using XDiary.Contracts;

internal sealed class Aggregate : IDailyMetricsAggregate
{
    public IDailyMetricsAnemicModel Model { get; }
    private Aggregate(IDailyMetricsAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IDailyMetricsAnemicModel model) => new(model);

    // IAggregate<TBC> / IAnemicModel<TBC>:
    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    // IDailyMetricsAnemicModel:
    public IDailyMetricsRoot Root => Model.Root;
    public int Stress => Model.Stress;
    public int RestingHr => Model.RestingHr;
    public int Hrv => Model.Hrv;
    public ISleep Sleep => Model.Sleep;
    public Guid? ClientId => Model.ClientId;
    private AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<IDailyMetrics, IDailyMetricsAnemicModel>.Commit<IDailyMetrics, IDailyMetricsAnemicModel>(Model, m); return new AggregateResultSuccess<IDailyMetrics, IDailyMetricsAnemicModel>(d); }
    private AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IDailyMetrics, IDailyMetricsAnemicModel>.Commit<IDailyMetrics, IDailyMetricsAnemicModel>(Model, Model); return new AggregateResultException<IDailyMetrics, IDailyMetricsAnemicModel>(d, new FailedSpecification<IDailyMetrics, IDailyMetricsAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Stress = Model.Stress, RestingHr = Model.RestingHr, Hrv = Model.Hrv, Sleep = Model.Sleep, ClientId = Model.ClientId };

    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordStress(string date, int stress, Guid clientId)
    {
        var dv = new DateFormatSpecification(); if (!dv.IsSatisfiedBy(date)) return Fail(dv.Reason);
        var nv = new NonNegativeSpecification(); if (!nv.IsSatisfiedBy(stress)) return Fail(nv.Reason);
        return Success(Clone() with { Root = DailyMetricsRoot.CreateInstance(date), Stress = stress, ClientId = clientId });
    }
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordRestingHr(string date, int restingHr, Guid clientId)
    {
        var dv = new DateFormatSpecification(); if (!dv.IsSatisfiedBy(date)) return Fail(dv.Reason);
        var nv = new NonNegativeSpecification(); if (!nv.IsSatisfiedBy(restingHr)) return Fail(nv.Reason);
        return Success(Clone() with { Root = DailyMetricsRoot.CreateInstance(date), RestingHr = restingHr, ClientId = clientId });
    }
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordHrv(string date, int hrv, Guid clientId)
    {
        var dv = new DateFormatSpecification(); if (!dv.IsSatisfiedBy(date)) return Fail(dv.Reason);
        var nv = new NonNegativeSpecification(); if (!nv.IsSatisfiedBy(hrv)) return Fail(nv.Reason);
        return Success(Clone() with { Root = DailyMetricsRoot.CreateInstance(date), Hrv = hrv, ClientId = clientId });
    }
    public AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordSleep(string date, ISleep sleep, Guid clientId)
    {
        var dv = new DateFormatSpecification(); if (!dv.IsSatisfiedBy(date)) return Fail(dv.Reason);
        return Success(Clone() with { Root = DailyMetricsRoot.CreateInstance(date), Sleep = sleep, ClientId = clientId });
    }
}