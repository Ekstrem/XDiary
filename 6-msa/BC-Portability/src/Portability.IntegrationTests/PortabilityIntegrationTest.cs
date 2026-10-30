using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Portability.Domain.Implementation;
using Portability.Storage;
using Xunit;

public class PortabilityIntegrationTest
{
    [Fact]
    public void Export_Then_Query()
    {
        var res = Aggregate.CreateInstance(new AnemicModel()).ExportRecords(new[] { "row1" });
        Assert.Equal(DomainOperationResultEnum.Success, res.Result);
        var entity = new CsvDocumentEntity { Id = res.Model().Id };
        PortabilityMapper.MapToEntity(res.Model(), entity);
        var readModel = PortabilityMapper.MapToReadModel(entity);
        Assert.Contains("id", readModel.Headers);
        Assert.Contains("row1", readModel.Rows);
    }
}