using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using AppleHealthGateway.Application.Commands;
using AppleHealthGateway.Domain;
using AppleHealthGateway.InternalContracts;
using MediatR;

namespace AppleHealthGateway.Application.Queries;

public class GetImportStateHandler : IRequestHandler<GetImportStateQuery, AppleImportReadModel?>
{
    private readonly IReadRepository<IAppleHealthGateway, AppleImportReadModel> _repository;

    public GetImportStateHandler(IReadRepository<IAppleHealthGateway, AppleImportReadModel> repository) => _repository = repository;

    public async Task<AppleImportReadModel?> Handle(GetImportStateQuery request, CancellationToken ct)
    {
        var id = AppleImportModelFactory.ToGuid(request.SourceId);
        return await _repository.GetByIdAsync(id, ct);
    }
}
