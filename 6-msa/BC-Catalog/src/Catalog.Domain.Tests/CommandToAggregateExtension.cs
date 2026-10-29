using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Events;

namespace Catalog.Domain.Tests;

public static class CommandToAggregateExtension
{
    public static ICommandToAggregate CreateCommandMetadata(this string commandName)
        => CommandToAggregate.Commit(
            Guid.NewGuid(),
            commandName,
            "test",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
