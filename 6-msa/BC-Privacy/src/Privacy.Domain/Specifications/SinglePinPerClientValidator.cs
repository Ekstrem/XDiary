namespace Privacy.Domain.Specifications;
using Privacy.Domain.Abstraction;
internal sealed class SinglePinPerClientValidator
{
    public bool IsSatisfiedBy(Guid clientId, IPinCredentialAnemicModel model)
    { return model.Root.ClientId == Guid.Empty || model.Root.ClientId == clientId; }
    public string Reason => "У клиента может быть только один PIN.";
}