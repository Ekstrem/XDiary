using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;

namespace Journal.Application;

/// <summary>
/// РџСЂРѕСЃС‚Р°СЏ СЂРµР°Р»РёР·Р°С†РёСЏ IPaging.
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
