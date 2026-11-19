namespace AppleHealthGateway.Domain.Implementation;

using AppleHealthGateway.Domain.Abstraction;

/// <summary>QG-102: начальное (пустое) состояние агрегата — фабрика с явным Id.</summary>
internal static class DefaultAnemicModel
{
    public static IAppleHealthGatewayAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
