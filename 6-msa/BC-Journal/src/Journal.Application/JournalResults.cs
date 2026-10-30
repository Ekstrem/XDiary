namespace Journal.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using XDiary.Contracts;
public static class JournalResults
{
    public static AggregateResult<IJournal, IJournalAnemicModel> Fail(string error)
    {
        var data = BusinessOperationData<IJournal, IJournalAnemicModel>.Commit<IJournal, IJournalAnemicModel>(default!, default!);
        return new AggregateResultException<IJournal, IJournalAnemicModel>(data, new FailedSpecification<IJournal, IJournalAnemicModel>(error));
    }
}