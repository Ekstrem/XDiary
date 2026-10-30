namespace Portability.Application.Queries;
using MediatR;
using Portability.InternalContracts;
public sealed record GetDocumentQuery : IRequest<CsvDocumentReadModel?>;
public sealed class GetDocumentHandler : IRequestHandler<GetDocumentQuery, CsvDocumentReadModel?>
{
    private readonly IPortabilityQueryRepository _repo;
    public GetDocumentHandler(IPortabilityQueryRepository repo) => _repo = repo;
    public Task<CsvDocumentReadModel?> Handle(GetDocumentQuery r, CancellationToken ct) => _repo.GetLastAsync(ct);
}