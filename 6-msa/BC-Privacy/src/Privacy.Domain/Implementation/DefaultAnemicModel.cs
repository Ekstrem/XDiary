namespace Privacy.Domain.Implementation;

using Privacy.Domain.Abstraction;

/// <summary>QG-102: начальное (пустое) состояние агрегата — фабрика с явным Id.</summary>
internal static class DefaultAnemicModel
{
    public static IPinCredentialAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
