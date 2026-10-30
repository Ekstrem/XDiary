namespace SomaticSeries.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;
using XDiary.Contracts;

public sealed class RecordPointHandler : IRequestHandler<RecordPointCommand, AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>>
{
    private readonly IRepository<ISomaticSeries, ISomaticSeriesAnemicModel> _repo;
    public RecordPointHandler(IRepository<ISomaticSeries, ISomaticSeriesAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>> Handle(RecordPointCommand r, CancellationToken ct)
    { var res = Aggregate.CreateInstance(new AnemicModel()).RecordPoint(r.T, r.Hr, r.BodyBattery); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}