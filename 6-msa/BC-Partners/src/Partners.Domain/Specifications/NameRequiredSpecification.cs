namespace Partners.Domain.Specifications;
using Partners.Domain.Abstraction;
internal sealed class NameRequiredSpecification
{
    public bool IsSatisfiedBy(IPartnerAnemicModel model) => !string.IsNullOrWhiteSpace(model.Root.Name);
    public string Reason => "РРјСЏ РїР°СЂС‚РЅС‘СЂР° РѕР±СЏР·Р°С‚РµР»СЊРЅРѕ.";
}