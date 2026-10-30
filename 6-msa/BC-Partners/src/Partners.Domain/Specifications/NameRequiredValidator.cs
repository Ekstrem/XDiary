namespace Partners.Domain.Specifications;
using Partners.Domain.Abstraction;
internal sealed class NameRequiredValidator
{
    public bool IsSatisfiedBy(IPartnerAnemicModel model) => !string.IsNullOrWhiteSpace(model.Root.Name);
    public string Reason => "Имя партнёра обязательно.";
}