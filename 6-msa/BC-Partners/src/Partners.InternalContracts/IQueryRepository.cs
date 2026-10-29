using Ardalis.Specification;

namespace Partners.InternalContracts;

/// <summary>
/// РЈРЅРёРІРµСЂСЃР°Р»СЊРЅС‹Р№ СЂРµРїРѕР·РёС‚РѕСЂРёР№ Р·Р°РїСЂРѕСЃРѕРІ (Ardalis Specification).
/// </summary>
public interface IQueryRepository<T> where T : class
{
    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);
    Task<T?> FindByAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);
}
