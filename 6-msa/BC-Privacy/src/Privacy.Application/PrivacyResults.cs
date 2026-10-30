namespace Privacy.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using XDiary.Contracts;
public static class PrivacyResults
{
    public static AggregateResult<IPrivacy, IPinCredentialAnemicModel> Fail(string error)
    { var d = BusinessOperationData<IPrivacy, IPinCredentialAnemicModel>.Commit<IPrivacy, IPinCredentialAnemicModel>(default!, default!); return new AggregateResultException<IPrivacy, IPinCredentialAnemicModel>(d, new FailedSpecification<IPrivacy, IPinCredentialAnemicModel>(error)); }
}