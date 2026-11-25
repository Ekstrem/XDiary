namespace Client.Domain.Specifications;
using Client.Domain.Abstraction;
public sealed class PhoneRequiredSpecification
{
    public bool IsSatisfiedBy(IClientAnemicModel model) => IsSatisfiedByPhone(model.Root.Phone);

    /// <summary>BE-149/QG-213: проверка входа без сборки доменной модели в хендлере.</summary>
    public static bool IsSatisfiedByPhone(string? phone) => !string.IsNullOrWhiteSpace(phone);

    public string Reason => "Номер телефона обязателен.";
}