namespace Portability.Domain.Implementation;

using Portability.Domain.Abstraction;

/// <summary>QG-102: начальное (пустое) состояние агрегата — фабрика с явным Id.</summary>
internal static class DefaultAnemicModel
{
    public static ICsvDocumentAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
