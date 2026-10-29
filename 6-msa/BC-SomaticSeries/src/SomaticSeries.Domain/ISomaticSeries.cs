using DigiTFactory.Libraries.SeedWorks.Definition;

namespace SomaticSeries.Domain;

/// <summary>
/// Ограниченный контекст «Партнёры».
/// Единственный агрегат контекста — SomaticPoint (профиль партнёра).
/// </summary>
public interface ISomaticSeries : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста партнёров.
/// </summary>
public class SomaticSeriesBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "SomaticSeries";
    public int MicroserviceVersion => 1;
}
