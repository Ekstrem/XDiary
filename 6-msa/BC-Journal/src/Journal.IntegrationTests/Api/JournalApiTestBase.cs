using Journal.IntegrationTests.Infrastructure;
using Xunit;

namespace Journal.IntegrationTests.Api;

[Collection(JournalApiCollectionDefinition.Name)]
public abstract class JournalApiTestBase
{
    protected JournalApiTestBase(JournalApiFixture fixture) => Fixture = fixture;
    protected JournalApiFixture Fixture { get; }
    protected bool Unavailable => !Fixture.Available;
}
