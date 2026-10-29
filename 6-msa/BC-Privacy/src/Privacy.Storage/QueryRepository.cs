using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Privacy.InternalContracts;
using Microsoft.EntityFrameworkCore;

namespace Privacy.Storage;

public class QueryRepository<T> : IQueryRepository<T> where T : class
{
    private readonly CommandDbContext _dbContext;

    public QueryRepository(CommandDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Set<T>().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
        => await _dbContext.Set<T>().WithSpecification(spec).ToListAsync(cancellationToken);

    public async Task<T?> FindByAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
        => await _dbContext.Set<T>().WithSpecification(spec).FirstOrDefaultAsync(cancellationToken);

    public async Task<int> CountAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
        => await _dbContext.Set<T>().WithSpecification(spec).CountAsync(cancellationToken);
}
