namespace SomaticSeries.Domain.Implementation;

using SomaticSeries.Domain.Abstraction;

/// <summary>QG-102: начальное (пустое) состояние агрегата — фабрика с явным Id.</summary>
internal static class DefaultAnemicModel
{
    public static ISomaticSeriesAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
