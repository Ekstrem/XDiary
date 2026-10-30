namespace Catalog.Domain.Specifications;
internal sealed class KindEnumValidator
{
    public bool IsSatisfiedBy(string kind) => kind is "costume" or "toy" or "fantasy" or "contraception";
    public string Reason => "kind должен быть costume|toy|fantasy|contraception.";
}