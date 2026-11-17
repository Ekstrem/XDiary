namespace Partners.Application;

/// <summary>Владелец/actor текущего запроса (берётся из авторизации JWT, не из тела команды — QG-209).</summary>
public interface IActorProvider
{
    Guid ClientId { get; }
}
