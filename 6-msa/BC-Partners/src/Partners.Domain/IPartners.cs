using DigiTFactory.Libraries.SeedWorks.Definition;

namespace Partners.Domain;

/// <summary>
/// Ограниченный контекст «Партнёры».
/// Единственный агрегат контекста — Partner (профиль партнёра).
/// </summary>
public interface IPartners : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста партнёров.
/// </summary>
public class PartnersBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "Partners";
    public int MicroserviceVersion => 1;
}
