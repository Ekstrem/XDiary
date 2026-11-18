namespace SomaticSeries.Application;

/// <summary>Владелец/actor текущего запроса (из JWT sub, не из тела команды — QG-209).</summary>
public interface IActorProvider
{
    Guid ClientId { get; }
}
