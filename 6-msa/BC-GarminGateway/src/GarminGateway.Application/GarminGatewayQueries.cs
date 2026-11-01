namespace GarminGateway.Application.Queries;
using MediatR;
using GarminGateway.InternalContracts;
public sealed record GetSyncStateQuery(Guid ClientId) : IRequest<GarminSyncReadModel?>;
public sealed class GetSyncStateHandler : IRequestHandler<GetSyncStateQuery, GarminSyncReadModel?>
{
    private readonly IGarminGatewayQueryRepository _repo;
    public GetSyncStateHandler(IGarminGatewayQueryRepository repo) => _repo = repo;
    public Task<GarminSyncReadModel?> Handle(GetSyncStateQuery r, CancellationToken ct) => _repo.GetByClientIdSourceIdAsync(r.ClientId, "garmin", ct);
}