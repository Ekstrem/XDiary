using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Catalog.Domain.Implementation;
using Catalog.Storage;
using Xunit;

public class CatalogIntegrationTest
{
    [Fact]
    public void Command_Then_Query()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball");
        var details = Aggregate.CreateInstance(start.Model()).UpdateDetails("photo", "note", "Brand", "");
        Assert.Equal(DomainOperationResultEnum.Success, details.Result);
        var entity = new CatalogItemEntity { Id = details.Model().Id };
        CatalogMapper.MapToEntity(details.Model(), entity);
        var readModel = CatalogMapper.MapToReadModel(entity);
        Assert.Equal("toy", readModel.Kind);
        Assert.Equal("Ball", readModel.Name);
        Assert.Equal("Brand", readModel.Brand);
    }
}