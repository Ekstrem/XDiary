namespace Journal.Application;

/// <summary>
/// QG-209: владелец/actor берётся из авторизации (JWT sub), а не из тела команды.
/// Реализацию (из HttpContext) даёт Api; Application зависит только от абстракции.
/// </summary>
public interface IActorProvider
{
    Guid ClientId { get; }
}
