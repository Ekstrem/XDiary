namespace GarminGateway.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using XDiary.Contracts;
public static class GarminGatewayResults
{
    public static AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Fail(string error)
    { var d = BusinessOperationData<IGarminGateway, IGarminGatewayAnemicModel>.Commit<IGarminGateway, IGarminGatewayAnemicModel>(default!, default!); return new AggregateResultException<IGarminGateway, IGarminGatewayAnemicModel>(d, new FailedSpecification<IGarminGateway, IGarminGatewayAnemicModel>(error)); }
}