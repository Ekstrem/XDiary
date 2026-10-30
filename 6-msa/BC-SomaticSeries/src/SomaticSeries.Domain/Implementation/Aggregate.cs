namespace SomaticSeries.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Specifications;
using XDiary.Contracts;

public sealed class Aggregate
{
    public ISomaticSeriesAnemicModel Model { get; }
    private Aggregate(ISomaticSeriesAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(ISomaticSeriesAnemicModel model) => new(model);

    private AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<ISomaticSeries, ISomaticSeriesAnemicModel>.Commit<ISomaticSeries, ISomaticSeriesAnemicModel>(Model, m); return new AggregateResultSuccess<ISomaticSeries, ISomaticSeriesAnemicModel>(d); }
    private AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> Fail(string e)
    { var d = BusinessOperationData<ISomaticSeries, ISomaticSeriesAnemicModel>.Commit<ISomaticSeries, ISomaticSeriesAnemicModel>(Model, Model); return new AggregateResultException<ISomaticSeries, ISomaticSeriesAnemicModel>(d, new FailedSpecification<ISomaticSeries, ISomaticSeriesAnemicModel>(e)); }

    public AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> RecordPoint(string t, int hr, int bodyBattery)
    {
        var mv = new MinuteGranularityValidator();
        if (!mv.IsSatisfiedBy(t)) return Fail(mv.Reason);
        var nv = new NonNegativeValidator();
        if (!nv.IsSatisfiedBy(hr, bodyBattery)) return Fail(nv.Reason);
        var m = new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = SomaticPointRoot.CreateInstance(t), Hr = hr, BodyBattery = bodyBattery };
        return Success(m);
    }
}