namespace AppleHealthGateway.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using XDiary.Contracts;
public static class AppleHealthGatewayResults
{
    public static AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> Fail(string error)
    { var d = BusinessOperationData<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>.Commit<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(default!, default!); return new AggregateResultException<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(d, new FailedSpecification<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(error)); }
}