using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using GarminGateway.Application.Commands;
using GarminGateway.Domain;
using GarminGateway.InternalContracts;
using MediatR;

namespace GarminGateway.Application.Queries;

public class GetSyncStateHandler : IRequestHandler<GetSyncStateQuery, GarminSyncReadModel?>
{
    private readonly IReadRepository<IGarminGateway, GarminSyncReadModel> _repository;

    public GetSyncStateHandler(IReadRepository<IGarminGateway, GarminSyncReadModel> repository) => _repository = repository;

    public async Task<GarminSyncReadModel?> Handle(GetSyncStateQuery request, CancellationToken ct)
    {
        var id = GarminSyncModelFactory.ToGuid(request.SourceId);
        return await _repository.GetByIdAsync(id, ct);
    }
}
