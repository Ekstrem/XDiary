using DigiTFactory.Libraries.SeedWorks.Definition;

namespace Analytics.Domain;

/// <summary>
/// Ограниченный контекст «Аналитика».
/// Read-only: нет агрегата записи — только вычисляемые проекции над Журналом и Соматикой.
/// </summary>
public interface IAnalytics : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста аналитики.
/// </summary>
public class AnalyticsBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "Analytics";
    public int MicroserviceVersion => 1;
}
