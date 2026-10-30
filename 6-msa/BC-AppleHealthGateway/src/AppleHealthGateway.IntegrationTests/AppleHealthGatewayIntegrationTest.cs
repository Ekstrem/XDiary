using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using AppleHealthGateway.Domain.Implementation;
using AppleHealthGateway.Storage;
using Xunit;

public class AppleHealthGatewayIntegrationTest
{
    [Fact]
    public void Configure_Then_Import()
    {
        var cfg = Aggregate.CreateInstance(new AnemicModel()).Configure(new[] { "hr", "hrv" });
        Assert.Equal(DomainOperationResultEnum.Success, cfg.Result);
        var imp = Aggregate.CreateInstance(cfg.Model()).ImportFile("export.xml");
        Assert.Equal(DomainOperationResultEnum.Success, imp.Result);
        var entity = new AppleImportEntity { Id = imp.Model().Id };
        AppleHealthGatewayMapper.MapToEntity(imp.Model(), entity);
        var readModel = AppleHealthGatewayMapper.MapToReadModel(entity);
        Assert.Equal("apple", readModel.SourceId);
        Assert.Contains("hr", readModel.Metrics);
        Assert.Contains("export.xml", readModel.ProcessedFiles);
    }
}