namespace Portability.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using Portability.Domain;
using Portability.InternalContracts;
public sealed record GetDocumentQuery(Guid ClientId) : IRequest<CsvDocumentReadModel?>;
public sealed class GetDocumentHandler : IRequestHandler<GetDocumentQuery, CsvDocumentReadModel?>, IQueryHandler
{
    private readonly IReadRepository<IPortability, CsvDocumentReadModel> _read;
    public GetDocumentHandler(IReadRepository<IPortability, CsvDocumentReadModel> read) => _read = read;
    public async Task<CsvDocumentReadModel?> Handle(GetDocumentQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).Where(x => x.ClientId == r.ClientId).LastOrDefault();
}
