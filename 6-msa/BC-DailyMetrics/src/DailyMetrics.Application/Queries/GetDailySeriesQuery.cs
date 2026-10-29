using DailyMetrics.InternalContracts;
using MediatR;

namespace DailyMetrics.Application.Queries;

/// <summary>Запрос: дневные тренды в диапазоне дат.</summary>
public class GetDailySeriesQuery : IRequest<List<DailyMetricsReadModel>>
{
    public string FromDate { get; init; } = string.Empty;
    public string ToDate { get; init; } = string.Empty;
}
