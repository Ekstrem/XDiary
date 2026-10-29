using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Partners.Domain;
using Partners.InternalContracts;
using MediatR;

namespace Partners.Application.Queries;

public class ListPartnersHandler : IRequestHandler<ListPartnersQuery, List<PartnersReadModel>>
{
    private readonly IReadRepository<IPartners, PartnersReadModel> _repository;

    public ListPartnersHandler(IReadRepository<IPartners, PartnersReadModel> repository) => _repository = repository;

    public async Task<List<PartnersReadModel>> Handle(ListPartnersQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 10000);

        var items = await _repository.GetAllAsync(new Paging(page, size), ct);
        return items.OrderBy(r => r.Name).ToList();
    }
}
