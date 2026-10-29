using DigiTFactory.Libraries.SeedWorks.Definition;

namespace Portability.Domain;

/// <summary>
/// Ограниченный контекст «Партнёры».
/// Единственный агрегат контекста — Portability (профиль партнёра).
/// </summary>
public interface IPortability : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста партнёров.
/// </summary>
public class PortabilityBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "Portability";
    public int MicroserviceVersion => 1;
}
