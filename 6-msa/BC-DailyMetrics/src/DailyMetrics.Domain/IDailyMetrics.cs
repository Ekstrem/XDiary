using DigiTFactory.Libraries.SeedWorks.Definition;

namespace DailyMetrics.Domain;

/// <summary>
/// Ограниченный контекст «Партнёры».
/// Единственный агрегат контекста — DailyMetrics (профиль партнёра).
/// </summary>
public interface IDailyMetrics : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста партнёров.
/// </summary>
public class DailyMetricsBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "DailyMetrics";
    public int MicroserviceVersion => 1;
}
