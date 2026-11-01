namespace Client.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Client.Domain;
using Client.Domain.Abstraction;
using XDiary.Contracts;
public static class ClientResults
{
    public static AggregateResult<IClients, IClientAnemicModel> Fail(string error)
    {
        var data = BusinessOperationData<IClients, IClientAnemicModel>.Commit<IClients, IClientAnemicModel>(default!, default!);
        return new AggregateResultException<IClients, IClientAnemicModel>(data, new FailedSpecification<IClients, IClientAnemicModel>(error));
    }
}