namespace Partners.Domain.Specifications;
internal sealed class GenderEnumValidator
{
    public string Normalize(string gender) => (gender ?? "").Trim().ToLowerInvariant();
    public bool IsSatisfiedBy(string gender) => Normalize(gender) is "male" or "female";
    public string Reason => "Пол должен быть male|female.";
}