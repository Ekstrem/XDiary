namespace Partners.Application.Commands;

using DigiTFactory.Libraries.SeedWorks.Events;

/// <summary>BE-150: создание CommandToAggregate на границе Application (timestamp = новая версия агрегата).</summary>
public static class AggregateCommandFactory
{
    public static CommandToAggregate Create(string commandName)
        => CommandToAggregate.Commit(Guid.NewGuid(), commandName, "Partners", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
