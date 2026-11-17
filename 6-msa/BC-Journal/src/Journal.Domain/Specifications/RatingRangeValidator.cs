namespace Journal.Domain.Specifications;

using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;

/// <summary>Оценка оргазма 0..5.</summary>
public sealed class RatingRangeValidator : IBusinessOperationAssertion<IJournal, IJournalAnemicModel>
{
    public bool IsSatisfiedBy(BusinessOperationData<IJournal, IJournalAnemicModel> obj)
        => obj.Model.Orgasms.All(orgasm => orgasm.Rating is >= 0 and <= 5);

    public string Reason => "RatingRange: rating must be in 0..5";

    public DomainOperationResultEnum DomainResult => DomainOperationResultEnum.Exception;
}
