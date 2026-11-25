using Journal.Domain;
using Journal.Domain.Implementation;
using Journal.Domain.Specifications;
using Xunit;

// BE-151 (DS DomainServicesJournalModelFactoryTests): фабрики модели и value objects.
public class JournalModelFactoryTests
{
    [Fact]
    public void AnemicModelCreate_When_IdProvided_Should_SetIdAndDefaults()
    {
        var id = Guid.NewGuid();
        var model = AnemicModel.Create(id);
        Assert.Equal(id, model.Id);
        Assert.Equal(RecordState.Draft, model.State);
        Assert.False(model.Deleted);
        Assert.Empty(model.Positions);
        Assert.Empty(model.Orgasms);
    }

    [Fact]
    public void DefaultAnemicModelCreate_When_IdProvided_Should_SetId()
    {
        var id = Guid.NewGuid();
        Assert.Equal(id, DefaultAnemicModel.Create(id).Id);
    }

    [Fact]
    public void RecordRootCreate_When_ListProvided_Should_CopyPartners()
    {
        var source = new List<string> { "p1", "p2" };
        var root = RecordRoot.CreateInstance(source, DateTimeOffset.Parse("2026-10-29T21:00+03:00"));
        source.Add("p3");
        Assert.Equal(new[] { "p1", "p2" }, root.Partners);
    }

    [Fact]
    public void ScalarFactories_When_Called_Should_ReturnTypedValues()
    {
        Assert.Equal("place", PlaceValue.Create("place").Value);
        Assert.Equal(new[] { "p" }, PositionsValue.Create(new[] { "p" }).Value);
        Assert.Equal(20, DurationValue.Create(20).Minutes);
        Assert.Equal(2, ContraceptionValue.Create(new[] { "c" }, 2).ContraceptiveCount);
    }

    [Fact]
    public void WhenIsoParser_When_ValidIso_Should_Normalize()
    {
        var parser = new WhenIsoParser();
        Assert.True(parser.TryParse("2026-10-29T21:00+03:00", out var normalized));
        Assert.Equal(DateTimeOffset.Parse("2026-10-29T18:00Z"), normalized);
    }

    [Fact]
    public void WhenIsoParser_When_Invalid_Should_Reject()
    {
        var parser = new WhenIsoParser();
        Assert.False(parser.TryParse("не дата", out _));
        Assert.False(string.IsNullOrWhiteSpace(parser.Reason));
    }

    [Fact]
    public void OrgasmsCreate_When_Called_Should_SetAllFields()
    {
        var value = Orgasms.CreateInstance("me", 2, new[] { "kind" }, 4);
        Assert.Equal("me", value.Participant);
        Assert.Equal(2, value.Count);
        Assert.Equal(new[] { "kind" }, value.Types);
        Assert.Equal(4, value.Rating);
    }

    [Fact]
    public void OriginValueCreate_When_Called_Should_SetGarminFields()
    {
        var value = OriginValue.Create(true, "g-7");
        Assert.True(value.Garmin);
        Assert.Equal("g-7", value.GarminId);
    }
}
