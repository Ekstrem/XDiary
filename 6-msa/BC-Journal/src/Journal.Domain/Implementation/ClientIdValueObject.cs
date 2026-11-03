namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// VO-обёртка скаляра ClientId, чтобы он попадал в change-track `GetValueObjects()`
/// и, как следствие, в конверт события (clientId обязателен по ADR-028 / event-delivery).
/// </summary>
public sealed record ClientIdValueObject(Guid? Value) : IValueObject;
