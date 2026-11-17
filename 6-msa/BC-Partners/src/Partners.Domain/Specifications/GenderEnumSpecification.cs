namespace Partners.Domain.Specifications;
internal sealed class GenderEnumSpecification
{
    public string Normalize(string gender) => (gender ?? "").Trim().ToLowerInvariant();
    public bool IsSatisfiedBy(string gender) => Normalize(gender) is "male" or "female";
    public string Reason => "РџРѕР» РґРѕР»Р¶РµРЅ Р±С‹С‚СЊ male|female.";
}