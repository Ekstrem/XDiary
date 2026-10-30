namespace Journal.Domain.Specifications;

using Journal.Domain.Abstraction;

internal sealed class RatingRangeValidator
{
    public bool IsSatisfiedBy(IOrgasms orgasm)
        => orgasm.Rating is >= 0 and <= 5;

    public string Reason => "Оценка оргазма должна быть в диапазоне 0..5.";
}
