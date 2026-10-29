using Ardalis.Specification;

namespace GarminGateway.Storage.Specifications;

public class GetByIdSpec : Specification<DomainEventEventEntry>
{
    public GetByIdSpec(Guid id)
    {
        Query
            .AsNoTracking()
            .Where(t => t.Id == id);
    }
}
