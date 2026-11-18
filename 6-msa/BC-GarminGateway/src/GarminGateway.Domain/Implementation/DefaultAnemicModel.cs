namespace GarminGateway.Domain.Implementation;

using GarminGateway.Domain.Abstraction;

/// <summary>QG-102: начальное (пустое) состояние агрегата — фабрика с явным Id.</summary>
internal static class DefaultAnemicModel
{
    public static IGarminGatewayAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
