using System.Reflection;
using Journal.Api.Controllers;
using Journal.Application.Commands;
using Journal.InternalContracts;
using Microsoft.AspNetCore.Mvc;
using Xunit;

// BE-151 (DS JournalContractsTests + AR §2.6.1): замороженные типы команд/read-модели и маршруты.
public class JournalContractsTests
{
    [Fact]
    public void PartnerCommands_When_Inspected_Should_UseOpaqueStringId()
    {
        Assert.Equal(typeof(string), typeof(AddPartnerCommand).GetProperty("PartnerId")!.PropertyType);
        Assert.Equal(typeof(string), typeof(RemovePartnerCommand).GetProperty("PartnerId")!.PropertyType);
    }

    [Fact]
    public void StartedAt_When_Inspected_Should_BeStringEverywhere()
    {
        Assert.Equal(typeof(string), typeof(SpecifyWhenCommand).GetProperty("StartedAt")!.PropertyType);
        Assert.Equal(typeof(string), typeof(StartFromGarminCommand).GetProperty("StartedAt")!.PropertyType);
        Assert.Equal(typeof(string), typeof(JournalReadModel).GetProperty("StartedAt")!.PropertyType);
    }

    [Fact]
    public void JournalCommands_When_Enumerated_Should_HaveThirteenUniqueNames()
    {
        var commands = new[]
        {
            typeof(StartManualEntryCommand), typeof(StartFromGarminCommand), typeof(SpecifyWhenCommand),
            typeof(AddPartnerCommand), typeof(RemovePartnerCommand), typeof(SpecifyPlaceCommand),
            typeof(AddPositionsCommand), typeof(AddAccessoriesCommand), typeof(RecordContraceptionCommand),
            typeof(SpecifyDurationCommand), typeof(RecordOrgasmsCommand), typeof(CompleteCommand), typeof(DeleteRecordCommand)
        };
        Assert.Equal(13, commands.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public void JournalReadModel_When_Inspected_Should_HaveIdVersionAndDeleted()
    {
        Assert.Equal(typeof(Guid), typeof(JournalReadModel).GetProperty("Id")!.PropertyType);
        Assert.Equal(typeof(long), typeof(JournalReadModel).GetProperty("Version")!.PropertyType);
        Assert.Equal(typeof(bool), typeof(JournalReadModel).GetProperty("Deleted")!.PropertyType);
    }

    [Fact]
    public void JournalController_When_RoutesCounted_Should_ExposeThirteenCommandsAndFiveQueries()
    {
        var actions = typeof(JournalController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var posts = actions.Count(a => a.GetCustomAttributes<HttpPostAttribute>().Any());
        var gets = actions.Count(a => a.GetCustomAttributes<HttpGetAttribute>().Any());
        Assert.Equal(13, posts);
        Assert.Equal(5, gets);
    }

    [Fact]
    public void JournalReadModel_When_Filled_Should_RoundTripColumns()
    {
        var id = Guid.NewGuid();
        var model = new JournalReadModel
        {
            Id = id,
            ClientId = Guid.NewGuid(),
            StartedAt = "2026-10-29T21:00",
            Place = "дома",
            Partners = new[] { "p1", "p2" },
            Positions = new[] { "pos" },
            Tags = new[] { "tag" },
            DurationMin = 25,
            Version = 42,
            Deleted = false
        };
        Assert.Equal(id, model.Id);
        Assert.Equal("2026-10-29T21:00", model.StartedAt);
        Assert.Equal(new[] { "p1", "p2" }, model.Partners);
        Assert.Equal(25, model.DurationMin);
        Assert.Equal(42, model.Version);
        Assert.False(model.Deleted);
    }
}
