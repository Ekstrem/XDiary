using Ardalis.Specification;

namespace Privacy.Storage.Specifications;

public class GetByCorrelationTokenSpec : Specification<DomainEventEventEntry>
{
    public GetByCorrelationTokenSpec(Guid correlationToken)
    {
        Query
            .AsNoTracking()
            .Where(t => t.CorrelationToken == correlationToken);
    }
}
