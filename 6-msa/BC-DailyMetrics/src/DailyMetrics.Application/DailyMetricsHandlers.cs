namespace DailyMetrics.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;
using XDiary.Contracts;
using DailyMetrics.InternalContracts;

public sealed class RecordStressHandler : IRequestHandler<RecordStressCommand, AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>
{
    private readonly IDailyMetricsRepository _repo;
    public RecordStressHandler(IDailyMetricsRepository repo) => _repo = repo;
    public async Task<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> Handle(RecordStressCommand r, CancellationToken ct)
    { var m = await _repo.GetByDateAsync(r.Date, ct) ?? new AnemicModel(); var res = Aggregate.CreateInstance(m).RecordStress(r.Date, r.Stress); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class RecordRestingHrHandler : IRequestHandler<RecordRestingHrCommand, AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>
{
    private readonly IDailyMetricsRepository _repo;
    public RecordRestingHrHandler(IDailyMetricsRepository repo) => _repo = repo;
    public async Task<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> Handle(RecordRestingHrCommand r, CancellationToken ct)
    { var m = await _repo.GetByDateAsync(r.Date, ct) ?? new AnemicModel(); var res = Aggregate.CreateInstance(m).RecordRestingHr(r.Date, r.RestingHr); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class RecordHrvHandler : IRequestHandler<RecordHrvCommand, AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>
{
    private readonly IDailyMetricsRepository _repo;
    public RecordHrvHandler(IDailyMetricsRepository repo) => _repo = repo;
    public async Task<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> Handle(RecordHrvCommand r, CancellationToken ct)
    { var m = await _repo.GetByDateAsync(r.Date, ct) ?? new AnemicModel(); var res = Aggregate.CreateInstance(m).RecordHrv(r.Date, r.Hrv); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class RecordSleepHandler : IRequestHandler<RecordSleepCommand, AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>
{
    private readonly IDailyMetricsRepository _repo;
    public RecordSleepHandler(IDailyMetricsRepository repo) => _repo = repo;
    public async Task<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> Handle(RecordSleepCommand r, CancellationToken ct)
    { var m = await _repo.GetByDateAsync(r.Date, ct) ?? new AnemicModel(); var sleep = Sleep.CreateInstance(r.Sleep.Score, r.Sleep.DurationMin, r.Sleep.DeepMin, r.Sleep.LightMin, r.Sleep.RemMin, r.Sleep.AwakeMin); var res = Aggregate.CreateInstance(m).RecordSleep(r.Date, sleep); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}