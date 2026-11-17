namespace GarminGateway.InternalContracts;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;

/// <summary>Простой IPaging (страница/размер) — для библиотечного IReadRepository.GetAllAsync.</summary>
public sealed record Paging(int Page, int PageSize) : IPaging;
