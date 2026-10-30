namespace DailyMetrics.Domain.Specifications;
internal sealed class NonNegativeValidator
{
    public bool IsSatisfiedBy(int value) => value >= 0;
    public string Reason => "Значение должно быть >= 0.";
}