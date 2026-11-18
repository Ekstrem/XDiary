namespace DailyMetrics.Domain.Specifications;
internal sealed class NonNegativeSpecification
{
    public bool IsSatisfiedBy(int value) => value >= 0;
    public string Reason => "Значение должно быть >= 0.";
}