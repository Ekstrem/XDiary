using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Partners.Domain;
using Partners.InternalContracts;
using MediatR;

namespace Partners.Application.Queries;

public class GetPartnerByIdHandler : IRequestHandler<GetPartnerByIdQuery, PartnersReadModel?>
{
    private readonly IReadRepository<IPartners, PartnersReadModel> _repository;

    public GetPartnerByIdHandler(IReadRepository<IPartners, PartnersReadModel> repository) => _repository = repository;

    public Task<PartnersReadModel?> Handle(GetPartnerByIdQuery request, CancellationToken ct)
        => _repository.GetByIdAsync(request.AggregateId, ct);
}
