using Portability.Application.Csv;
using Xunit;

namespace Portability.Domain.Tests;

/// <summary>
/// Тесты CSV-сериализации (Published Language).
/// </summary>
public class CsvSerializerTest
{
    [Fact]
    public void Serialize_IncludesFixedHeaders()
    {
        var csv = CsvSerializer.Serialize(new List<string[]> { new[] { "1", "2026-10-29T21:00", "30" } });

        Assert.StartsWith(string.Join(',', CsvHeaders.Record), csv);
    }

    [Fact]
    public void Roundtrip_PreservesRows()
    {
        var rows = new List<string[]>
        {
            new[] { "1", "2026-10-29T21:00", "30" },
            new[] { "2", "2026-10-30T22:00", "45" }
        };

        var csv = CsvSerializer.Serialize(rows);
        var (headers, parsed) = CsvSerializer.Deserialize(csv);

        Assert.True(CsvSerializer.HasValidHeaders(headers));
        Assert.Equal(2, parsed.Count);
        Assert.Equal("1", parsed[0][0]);
        Assert.Equal("45", parsed[1][2]);
    }

    [Fact]
    public void ListSeparator_JoinsAndSplits()
    {
        var joined = CsvSerializer.JoinList(new[] { "missionary", "cowgirl" });

        Assert.Equal("missionary;cowgirl", joined);
        Assert.Equal(new[] { "missionary", "cowgirl" }, CsvSerializer.SplitList(joined));
    }

    [Fact]
    public void HasValidHeaders_MatchesContract()
    {
        Assert.True(CsvSerializer.HasValidHeaders(CsvHeaders.Record));
        Assert.False(CsvSerializer.HasValidHeaders(new[] { "id", "name" }));
    }
}
