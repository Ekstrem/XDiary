using DigiTFactory.Libraries.SeedWorks.Definition;

namespace Privacy.Domain;

/// <summary>
/// Ограниченный контекст «Партнёры».
/// Единственный агрегат контекста — PinCredential (профиль партнёра).
/// </summary>
public interface IPrivacy : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста партнёров.
/// </summary>
public class PrivacyBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "Privacy";
    public int MicroserviceVersion => 1;
}
