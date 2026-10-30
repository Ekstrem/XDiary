using DigiTFactory.Libraries.SeedWorks.Result;
using Portability.Application.Csv;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;
using Xunit;

namespace XDiary.Tests.PortabilityGateway;

/// <summary>TE-006: portability — CSV round-trip без потерь (списки через ;) + контракт колонок.</summary>
public class PortabilityTests
{
    [Fact]
    public void Csv_RoundTrip_PreservesRows_AndSemicolonLists()
    {
        var rows = new List<string>
        {
            "id1,2026-10-29T21:00,20,partner-1,дома,Миссионерская;Ложки,,костюм,вибратор,ролевая,презерватив,2,1,2,обычный,5,клиторальный,4,0"
        };

        var csv = CsvSerializer.Serialize(CsvHeaderContract.All, rows);
        var back = CsvSerializer.Deserialize(csv);

        Assert.Single(back);
        Assert.Equal(rows[0], back[0]);
        Assert.Contains("Миссионерская;Ложки", back[0]);
    }

    [Fact]
    public void Csv_RoundTrip_Empty_ReturnsNoRows()
        => Assert.Empty(CsvSerializer.Deserialize(""));

    [Fact]
    public void HeaderContract_MatchesAppCsvHeaders()
    {
        Assert.Equal(19, CsvHeaderContract.All.Count);
        Assert.Equal("id", CsvHeaderContract.All[0]);
        Assert.Equal("garmin", CsvHeaderContract.All[^1]);
        Assert.Contains("contraceptives", CsvHeaderContract.All);
        Assert.Contains("contraceptive_count", CsvHeaderContract.All);
        Assert.Contains("orgasm_me_type", CsvHeaderContract.All);
        Assert.Contains("orgasm_me_rating", CsvHeaderContract.All);
    }

    [Fact]
    public void ImportRecords_CanonicalHeaders_Succeeds()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).ImportRecords(new[] { "row-1" });
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void ExportRecords_ReturnsRows()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).ExportRecords(new[] { "a", "b" });
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }
}
