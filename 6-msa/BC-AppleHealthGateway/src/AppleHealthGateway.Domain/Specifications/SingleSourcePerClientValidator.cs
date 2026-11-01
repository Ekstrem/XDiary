namespace AppleHealthGateway.Domain.Specifications;
using AppleHealthGateway.Domain.Abstraction;
internal sealed class SingleSourcePerClientValidator
{
    public bool IsSatisfiedBy(Guid clientId, IAppleHealthGatewayAnemicModel model)
    {
        if (model.Root is null) return true;
        return model.Root.ClientId == Guid.Empty || model.Root.ClientId == clientId;
    }
    public string Reason => "У клиента может быть только один Apple Health-источник.";
}