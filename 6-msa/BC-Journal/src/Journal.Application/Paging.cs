using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;

namespace Journal.Application;

/// <summary>
/// Простая реализация IPaging.
/// </summary>
public sealed class Paging : IPaging
{
    public Paging(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }
    public int PageSize { get; }
}
