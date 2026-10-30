using DigiTFactory.Libraries.SeedWorks.Definition;

namespace Journal.Domain;

/// <summary>
/// Ограниченный контекст «Партнёры».
/// Единственный агрегат контекста — Record (профиль партнёра).
/// </summary>
public interface IJournal : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста партнёров.
/// </summary>
public class JournalBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "Journal";
    public int MicroserviceVersion => 1;
}
