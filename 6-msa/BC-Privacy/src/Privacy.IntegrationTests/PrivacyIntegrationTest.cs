using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Privacy.Application.Security;
using Privacy.Domain.Implementation;
using Privacy.Storage;
using Xunit;

public class PrivacyIntegrationTest
{
    [Fact]
    public void SetPin_Then_Verify()
    {
        var (salt, hash, iter) = PinHasher.Hash("1234");
        var set = Aggregate.CreateInstance(new AnemicModel()).SetPin(salt, hash, iter);
        Assert.Equal(DomainOperationResultEnum.Success, set.Result);
        var entity = new PinCredentialEntity { Id = set.Model().Id };
        PrivacyMapper.MapToEntity(set.Model(), entity);
        var readModel = PrivacyMapper.MapToReadModel(entity);
        Assert.True(readModel.HasPin);
        Assert.True(PinHasher.Verify("1234", readModel.Salt, readModel.Hash, readModel.Iterations));
        Assert.False(PinHasher.Verify("9999", readModel.Salt, readModel.Hash, readModel.Iterations));
    }
}