namespace Journal.Domain.Specifications;

using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;

/// <summary>Длительность должна быть положительной.</summary>
public sealed class DurationPositiveValidator : IBusinessOperationAssertion<IJournal, IJournalAnemicModel>
{
    public bool IsSatisfiedBy(BusinessOperationData<IJournal, IJournalAnemicModel> obj)
        => obj.Model.DurationMin >= 1;

    public string Reason => "DurationPositive: durationMin must be >= 1";

    public DomainOperationResultEnum DomainResult => DomainOperationResultEnum.Exception;
}
