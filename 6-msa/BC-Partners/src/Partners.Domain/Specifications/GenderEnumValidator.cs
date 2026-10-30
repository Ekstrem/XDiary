namespace Partners.Domain.Specifications;
internal sealed class GenderEnumValidator
{
    public bool IsSatisfiedBy(string gender) => gender is "male" or "female";
    public string Reason => "Пол должен быть male|female.";
}