using Ardalis.Specification;

namespace Privacy.Storage.Specifications;

public class GetByIdSpec : Specification<DomainEventEventEntry>
{
    public GetByIdSpec(Guid id)
    {
        Query
            .AsNoTracking()
            .Where(t => t.Id == id);
    }
}
