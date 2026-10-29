using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Portability.Application.Commands;
using Portability.Domain;
using Portability.InternalContracts;
using MediatR;

namespace Portability.Application.Queries;

public class GetDocumentHandler : IRequestHandler<GetDocumentQuery, CsvDocumentReadModel?>
{
    private readonly IReadRepository<IPortability, CsvDocumentReadModel> _repository;

    public GetDocumentHandler(IReadRepository<IPortability, CsvDocumentReadModel> repository) => _repository = repository;

    public async Task<CsvDocumentReadModel?> Handle(GetDocumentQuery request, CancellationToken ct)
    {
        var id = CsvDocumentModelFactory.ToGuid(request.SourceId);
        return await _repository.GetByIdAsync(id, ct);
    }
}
