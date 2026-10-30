using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using AppleHealthGateway.Domain.Implementation;
using Xunit;

public class AppleImportAggregateTest
{
    [Fact]
    public void Configure_Valid_Succeeds()
    { var r = Aggregate.CreateInstance(new AnemicModel()).Configure(new[] { "hr", "hrv" }); Assert.Equal(DomainOperationResultEnum.Success, r.Result); }
    [Fact]
    public void ImportFile_Adds_File()
    {
        var cfg = Aggregate.CreateInstance(new AnemicModel()).Configure(new[] { "hr" });
        var r = Aggregate.CreateInstance(cfg.Model()).ImportFile("export.xml");
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        Assert.Contains("export.xml", r.Model().ProcessedFiles);
    }
    [Fact]
    public void ImportFile_Duplicate_Skips()
    {
        var cfg = Aggregate.CreateInstance(new AnemicModel()).Configure(new[] { "hr" });
        var first = Aggregate.CreateInstance(cfg.Model()).ImportFile("export.xml");
        var second = Aggregate.CreateInstance(first.Model()).ImportFile("export.xml");
        Assert.Equal(DomainOperationResultEnum.Success, second.Result);
        Assert.Single(second.Model().ProcessedFiles);
    }
}