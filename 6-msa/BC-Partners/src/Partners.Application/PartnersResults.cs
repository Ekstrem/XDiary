namespace Partners.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Domain;
using Partners.Domain.Abstraction;
using XDiary.Contracts;
public static class PartnersResults
{
    public static AggregateResult<IPartners, IPartnerAnemicModel> Fail(string error)
    {
        var data = BusinessOperationData<IPartners, IPartnerAnemicModel>.Commit<IPartners, IPartnerAnemicModel>(default!, default!);
        return new AggregateResultException<IPartners, IPartnerAnemicModel>(data, new FailedSpecification<IPartners, IPartnerAnemicModel>(error));
    }
}