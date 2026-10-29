using SomaticSeries.InternalContracts;
using MediatR;

namespace SomaticSeries.Application.Queries;

/// <summary>Запрос: срез соматики вокруг startedAt (±WindowMin минут).</summary>
public class GetSomaticSliceQuery : IRequest<List<SomaticSeriesReadModel>>
{
    public string StartedAt { get; init; } = string.Empty;
    public int WindowMin { get; init; } = 15;
}
