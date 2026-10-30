namespace Portability.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Portability.Domain;
using Portability.Domain.Abstraction;
using XDiary.Contracts;
public static class PortabilityResults
{
    public static AggregateResult<IPortability, ICsvDocumentAnemicModel> Fail(string error)
    { var d = BusinessOperationData<IPortability, ICsvDocumentAnemicModel>.Commit<IPortability, ICsvDocumentAnemicModel>(default!, default!); return new AggregateResultException<IPortability, ICsvDocumentAnemicModel>(d, new FailedSpecification<IPortability, ICsvDocumentAnemicModel>(error)); }
}