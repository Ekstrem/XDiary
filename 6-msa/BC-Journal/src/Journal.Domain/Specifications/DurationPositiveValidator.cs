namespace Journal.Domain.Specifications;

internal sealed class DurationPositiveValidator
{
    public bool IsSatisfiedBy(int durationMin)
        => durationMin >= 1;

    public string Reason => "Длительность (durationMin) должна быть >= 1.";
}
