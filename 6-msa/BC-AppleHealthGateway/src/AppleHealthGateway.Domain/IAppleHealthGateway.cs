using DigiTFactory.Libraries.SeedWorks.Definition;

namespace AppleHealthGateway.Domain;

/// <summary>
/// Ограниченный контекст «Партнёры».
/// Единственный агрегат контекста — AppleHealthGateway (профиль партнёра).
/// </summary>
public interface IAppleHealthGateway : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста партнёров.
/// </summary>
public class AppleHealthGatewayBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "AppleHealthGateway";
    public int MicroserviceVersion => 1;
}
