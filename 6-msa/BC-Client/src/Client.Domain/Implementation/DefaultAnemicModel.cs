namespace Client.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Client.Domain.Abstraction;

/// <summary>QG-102: начальное (пустое) состояние агрегата — фабрика с явным Id.</summary>
internal static class DefaultAnemicModel
{
    public static IClientAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
