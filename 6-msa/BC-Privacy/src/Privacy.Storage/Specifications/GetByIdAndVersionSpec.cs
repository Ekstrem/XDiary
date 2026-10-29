using Ardalis.Specification;

namespace Privacy.Storage.Specifications;

public class GetByIdAndVersionSpec : Specification<DomainEventEventEntry>
{
    public GetByIdAndVersionSpec(Guid id, long version)
    {
        Query
            .AsNoTracking()
            .Where(t => t.Id == id && t.Version == version);
    }
}
