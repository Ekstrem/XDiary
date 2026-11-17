namespace Journal.Domain.Specifications;

using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;

/// <summary>Канвас §3: Delete применим только к завершённой записи (Recorded).</summary>
public sealed class RecordDeletableValidator : IBusinessOperationAssertion<IJournal, IJournalAnemicModel>
{
    public bool IsSatisfiedBy(BusinessOperationData<IJournal, IJournalAnemicModel> obj)
        => obj.Aggregate.State == RecordState.Recorded;

    public string Reason => "RecordDeletable: delete applies only to a Recorded record";

    public DomainOperationResultEnum DomainResult => DomainOperationResultEnum.Exception;
}
