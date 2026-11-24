namespace SomaticSeries.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Specifications;
using XDiary.Contracts;

internal sealed class Aggregate : ISomaticSeriesAggregate
{
    public ISomaticSeriesAnemicModel Model { get; }
    private Aggregate(ISomaticSeriesAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(ISomaticSeriesAnemicModel model) => new(model);

    // IAggregate<TBC> / IAnemicModel<TBC>:
    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    // ISomaticSeriesAnemicModel:
    public ISomaticPointRoot Root => Model.Root;
    public int Hr => Model.Hr;
    public int BodyBattery => Model.BodyBattery;
    public Guid? ClientId => Model.ClientId;
    private AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> Success(AnemicModel m, [System.Runtime.CompilerServices.CallerMemberName] string commandName = "")
    {
        var command = CommandToAggregate.Commit(m.CorrelationToken, commandName, m.SubjectName, Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Model.Version + 1));
        m = m with { Version = command.Version, CommandName = command.CommandName, SubjectName = command.SubjectName }; // BE-150: Command.Version = новая версия агрегата (timestamp), acted-on Version остаётся в DomainEvent.Version
        var d = BusinessOperationData<ISomaticSeries, ISomaticSeriesAnemicModel>.Commit<ISomaticSeries, ISomaticSeriesAnemicModel>(Model, m); return new AggregateResultSuccess<ISomaticSeries, ISomaticSeriesAnemicModel>(d); }
    private AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> Fail(string e)
    { var d = BusinessOperationData<ISomaticSeries, ISomaticSeriesAnemicModel>.Commit<ISomaticSeries, ISomaticSeriesAnemicModel>(Model, Model); return new AggregateResultException<ISomaticSeries, ISomaticSeriesAnemicModel>(d, new FailedSpecification<ISomaticSeries, ISomaticSeriesAnemicModel>(e)); }

    public AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> RecordPoint(string t, int hr, int bodyBattery, Guid clientId)
    {
        var mv = new MinuteGranularitySpecification();
        if (!mv.IsSatisfiedBy(t)) return Fail(mv.Reason);
        var nv = new NonNegativeSpecification();
        if (!nv.IsSatisfiedBy(hr, bodyBattery)) return Fail(nv.Reason);
        var m = new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = SomaticPointRoot.CreateInstance(t), Hr = hr, BodyBattery = bodyBattery, ClientId = clientId };
        return Success(m);
    }
}