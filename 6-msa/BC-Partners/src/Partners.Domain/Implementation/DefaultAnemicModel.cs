namespace Partners.Domain.Implementation;

using Partners.Domain.Abstraction;

/// <summary>QG-102: начальное (пустое) состояние агрегата — фабрика с явным Id.</summary>
internal static class DefaultAnemicModel
{
    public static IPartnerAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
