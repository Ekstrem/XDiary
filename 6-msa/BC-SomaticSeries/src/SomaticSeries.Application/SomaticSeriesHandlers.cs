namespace SomaticSeries.Application.Commands;
using SomaticSeries.Application;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;
using XDiary.Contracts;

public sealed class RecordPointHandler : IRequestHandler<RecordPointCommand, AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>>
{
    private readonly IRepository<ISomaticSeries, ISomaticSeriesAnemicModel> _repo;
    private readonly ISomaticSeriesEventPublisher _publisher;
    public RecordPointHandler(IRepository<ISomaticSeries, ISomaticSeriesAnemicModel> repo, ISomaticSeriesEventPublisher publisher) { _repo = repo; _publisher = publisher; }
    public async Task<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>> Handle(RecordPointCommand r, CancellationToken ct)
    { var res = Aggregate.CreateInstance(new AnemicModel()).RecordPoint(r.T, r.Hr, r.BodyBattery); if (res.IsSuccess()) { await _repo.SaveAsync(res, ct); await _publisher.PublishPointAsync(r.T, r.Hr, r.BodyBattery, ct); } return res; }
}