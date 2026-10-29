using DigiTFactory.Libraries.SeedWorks.Definition;

namespace GarminGateway.Domain;

/// <summary>
/// Ограниченный контекст «Партнёры».
/// Единственный агрегат контекста — GarminGateway (профиль партнёра).
/// </summary>
public interface IGarminGateway : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста партнёров.
/// </summary>
public class GarminGatewayBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "GarminGateway";
    public int MicroserviceVersion => 1;
}
