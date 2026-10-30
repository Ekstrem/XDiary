namespace AppleHealthGateway.Application.Queries;
using MediatR;
using AppleHealthGateway.InternalContracts;
public sealed record GetImportStateQuery(string SourceId) : IRequest<AppleImportReadModel?>;
public sealed class GetImportStateHandler : IRequestHandler<GetImportStateQuery, AppleImportReadModel?>
{
    private readonly IAppleHealthGatewayQueryRepository _repo;
    public GetImportStateHandler(IAppleHealthGatewayQueryRepository repo) => _repo = repo;
    public Task<AppleImportReadModel?> Handle(GetImportStateQuery r, CancellationToken ct) => _repo.GetBySourceIdAsync(r.SourceId, ct);
}