namespace GarminGateway.Domain.Specifications;
using GarminGateway.Domain.Abstraction;
internal sealed class SingleSourcePerClientValidator
{
    public bool IsSatisfiedBy(Guid clientId, IGarminGatewayAnemicModel model)
    {
        // Один источник на клиента: если модель уже имеет источник, он должен принадлежать этому же клиенту.
        if (model.Root is null) return true;
        return model.Root.ClientId == Guid.Empty || model.Root.ClientId == clientId;
    }
    public string Reason => "У клиента может быть только один Garmin-источник.";
}