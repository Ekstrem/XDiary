namespace GarminGateway.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using GarminGateway.Domain;
using GarminGateway.InternalContracts;
public sealed record GetSyncStateQuery(Guid ClientId) : IRequest<GarminSyncReadModel?>;
public sealed class GetSyncStateHandler : IRequestHandler<GetSyncStateQuery, GarminSyncReadModel?>, IQueryHandler
{
    private readonly IReadRepository<IGarminGateway, GarminSyncReadModel> _read;
    public GetSyncStateHandler(IReadRepository<IGarminGateway, GarminSyncReadModel> read) => _read = read;
    public async Task<GarminSyncReadModel?> Handle(GetSyncStateQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).FirstOrDefault(x => x.ClientId == r.ClientId && x.SourceId == "garmin");
}
