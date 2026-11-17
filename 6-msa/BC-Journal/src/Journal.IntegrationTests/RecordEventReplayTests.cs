using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Implementation;
using Journal.Storage;
using Xunit;

/// <summary>
/// Канон §10 (FullEventSourcing): replay сворачивает ChangedValueObjects событий в AnemicModel.
/// </summary>
public class RecordEventReplayTests
{
    [Fact]
    public void Apply_FoldsEvents_ReconstructsRecord()
    {
        var id = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var startedAt = DateTimeOffset.Parse("2026-10-29T21:00");

        var model = new AnemicModel { Id = id };

        // StartManualEntry: Root + ClientId + State=Draft.
        model = RecordEventReplay.Apply(model, new Dictionary<string, IValueObject>
        {
            ["Root"] = RecordRoot.CreateInstance(Array.Empty<string>(), default),
            ["ClientId"] = new ClientIdValueObject(clientId),
            ["State"] = new IntValueObject((int)RecordState.Draft),
        }, new DomainEventEntry { Id = id, Version = 1 });

        // SpecifyWhen: Root со startedAt.
        model = RecordEventReplay.Apply(model, new Dictionary<string, IValueObject>
        {
            ["Root"] = RecordRoot.CreateInstance(Array.Empty<string>(), startedAt),
        }, new DomainEventEntry { Id = id, Version = 2 });

        // Complete: State = Recorded.
        model = RecordEventReplay.Apply(model, new Dictionary<string, IValueObject>
        {
            ["State"] = new IntValueObject((int)RecordState.Recorded),
        }, new DomainEventEntry { Id = id, Version = 3 });

        Assert.Equal(clientId, model.ClientId);
        Assert.Equal(startedAt, model.Root.StartedAt);
        Assert.Equal(RecordState.Recorded, model.State);
        Assert.Equal(3, model.Version);
    }
}
