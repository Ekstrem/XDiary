namespace Journal.Domain.Specifications;

using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;

internal sealed class WhenRequiredValidator : IBusinessOperationAssertion<IJournal, IJournalAnemicModel>
{
    public bool IsSatisfiedBy(BusinessOperationData<IJournal, IJournalAnemicModel> obj)
        => obj.Model.Root.StartedAt != default;

    public string Reason => "Дата (startedAt) обязательна до Complete.";

    public DomainOperationResultEnum DomainResult => DomainOperationResultEnum.Exception;
}
