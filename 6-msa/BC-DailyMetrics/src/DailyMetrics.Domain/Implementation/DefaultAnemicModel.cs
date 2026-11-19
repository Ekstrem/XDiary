namespace DailyMetrics.Domain.Implementation;

using DailyMetrics.Domain.Abstraction;

/// <summary>QG-102: начальное (пустое) состояние агрегата — фабрика с явным Id.</summary>
internal static class DefaultAnemicModel
{
    public static IDailyMetricsAnemicModel Create(Guid id) => AnemicModel.Create(id);
}
