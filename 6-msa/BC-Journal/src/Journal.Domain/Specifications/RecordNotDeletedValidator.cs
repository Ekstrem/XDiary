namespace Journal.Domain.Specifications;

using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;

/// <summary>Удалённая запись не принимает команды.</summary>
public sealed class RecordNotDeletedValidator : IBusinessOperationAssertion<IJournal, IJournalAnemicModel>
{
    public bool IsSatisfiedBy(BusinessOperationData<IJournal, IJournalAnemicModel> obj)
        => obj.Aggregate.State != RecordState.Deleted;

    public string Reason => "RecordNotDeleted: record is already deleted";

    public DomainOperationResultEnum DomainResult => DomainOperationResultEnum.Exception;
}
