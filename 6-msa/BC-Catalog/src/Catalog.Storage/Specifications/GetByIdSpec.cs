using Ardalis.Specification;

namespace Catalog.Storage.Specifications;

public class GetByIdSpec : Specification<DomainEventEventEntry>
{
    public GetByIdSpec(Guid id)
    {
        Query
            .AsNoTracking()
            .Where(t => t.Id == id);
    }
}
