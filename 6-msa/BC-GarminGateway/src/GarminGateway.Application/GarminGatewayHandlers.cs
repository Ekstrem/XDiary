namespace GarminGateway.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using GarminGateway.InternalContracts;
using XDiary.Contracts;

public sealed class ConnectHandler : IRequestHandler<ConnectCommand, AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>
{
    private readonly IGarminGatewayRepository _repo;
    public ConnectHandler(IGarminGatewayRepository repo) => _repo = repo;
    public async Task<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> Handle(ConnectCommand r, CancellationToken ct)
    { var m = await _repo.GetBySourceIdAsync("garmin", ct) ?? new AnemicModel(); var creds = Credentials.CreateInstance(r.Credentials.ApiKey, r.Credentials.OauthToken, r.Credentials.TokenExpiry); var res = Aggregate.CreateInstance(m).Connect(creds); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class SyncHandler : IRequestHandler<SyncCommand, AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>
{
    private readonly IGarminGatewayRepository _repo;
    public SyncHandler(IGarminGatewayRepository repo) => _repo = repo;
    public async Task<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> Handle(SyncCommand r, CancellationToken ct)
    { var m = await _repo.GetBySourceIdAsync("garmin", ct) ?? new AnemicModel(); var cfg = SyncConfig.CreateInstance(r.Config.Metrics, r.Config.WindowDays); var res = Aggregate.CreateInstance(m).Sync(cfg); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class FetchActivitiesHandler : IRequestHandler<FetchActivitiesCommand, AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>
{
    private readonly IGarminGatewayRepository _repo;
    public FetchActivitiesHandler(IGarminGatewayRepository repo) => _repo = repo;
    public async Task<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> Handle(FetchActivitiesCommand r, CancellationToken ct)
    { var m = await _repo.GetBySourceIdAsync("garmin", ct) ?? new AnemicModel(); var res = Aggregate.CreateInstance(m).FetchActivities(); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}