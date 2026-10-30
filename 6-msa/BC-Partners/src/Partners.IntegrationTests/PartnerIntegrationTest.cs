using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Domain.Implementation;
using Partners.Storage;
using Xunit;

public class PartnerIntegrationTest
{
    [Fact]
    public void Command_Then_Query()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("Alice");
        var profile = Aggregate.CreateInstance(start.Model()).UpdateProfile("photo", "female", "note");
        Assert.Equal(DomainOperationResultEnum.Success, profile.Result);
        var entity = new PartnerEntity { Id = profile.Model().Id };
        PartnerMapper.MapToEntity(profile.Model(), entity);
        var readModel = PartnerMapper.MapToReadModel(entity);
        Assert.Equal("Alice", readModel.Name);
        Assert.Equal("female", readModel.Gender);
    }
}