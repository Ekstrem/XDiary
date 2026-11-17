namespace Journal.Domain.Specifications;

using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;

/// <summary>Канон §6: менять запись можно только в состоянии Draft.</summary>
public sealed class RecordIsDraftValidator : IBusinessOperationAssertion<IJournal, IJournalAnemicModel>
{
    public bool IsSatisfiedBy(BusinessOperationData<IJournal, IJournalAnemicModel> obj)
        => obj.Aggregate.State == RecordState.Draft;

    public string Reason => "RecordIsDraft: record can be changed only in Draft state";

    public DomainOperationResultEnum DomainResult => DomainOperationResultEnum.Exception;
}
