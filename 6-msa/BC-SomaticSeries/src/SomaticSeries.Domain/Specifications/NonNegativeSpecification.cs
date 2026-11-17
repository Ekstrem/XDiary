namespace SomaticSeries.Domain.Specifications;
internal sealed class NonNegativeSpecification
{
    public bool IsSatisfiedBy(int hr, int bodyBattery) => hr >= 0 && bodyBattery >= 0;
    public string Reason => "hr и body_battery должны быть >= 0.";
}