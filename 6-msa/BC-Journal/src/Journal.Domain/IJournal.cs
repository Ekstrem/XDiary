using DigiTFactory.Libraries.SeedWorks.Definition;

namespace Journal.Domain;

/// <summary>
/// Ограниченный контекст «Журнал интимной активности».
/// Единственный агрегат контекста — Record (запись/акт).
/// </summary>
public interface IJournal : IBoundedContext { }

/// <summary>
/// Описание ограниченного контекста журнала.
/// </summary>
public class JournalBoundedContextDescription : IBoundedContextDescription
{
    public string ContextName => "Journal";
    public int MicroserviceVersion => 1;
}
