using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using GarminGateway.Domain.Implementation;
using GarminGateway.Storage;
using Xunit;

public class GarminGatewayIntegrationTest
{
    [Fact]
    public void Connect_Then_Query()
    {
        var connect = Aggregate.CreateInstance(new AnemicModel()).Connect(Credentials.CreateInstance("key", "token", "2026-10-30T00:00"));
        Assert.Equal(DomainOperationResultEnum.Success, connect.Result);
        var sync = Aggregate.CreateInstance(connect.Model()).Sync(SyncConfig.CreateInstance(new[] { "hr", "bb" }, 30));
        Assert.Equal(DomainOperationResultEnum.Success, sync.Result);
        var entity = new GarminSyncEntity { Id = sync.Model().Id };
        GarminGatewayMapper.MapToEntity(sync.Model(), entity);
        var readModel = GarminGatewayMapper.MapToReadModel(entity);
        Assert.Equal("garmin", readModel.SourceId);
        Assert.Equal(new[] { "hr", "bb" }, readModel.Metrics);
        Assert.Equal(30, readModel.WindowDays);
    }
}