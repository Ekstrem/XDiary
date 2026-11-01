namespace Client.Domain.Specifications;
using Client.Domain.Abstraction;
internal sealed class PhoneRequiredValidator
{
    public bool IsSatisfiedBy(IClientAnemicModel model) => !string.IsNullOrWhiteSpace(model.Root.Phone);
    public string Reason => "Номер телефона обязателен.";
}