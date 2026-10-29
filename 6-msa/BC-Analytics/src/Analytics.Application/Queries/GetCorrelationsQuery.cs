using Analytics.Domain.Models;
using MediatR;

namespace Analytics.Application.Queries;

/// <summary>Запрос: корреляции (Pearson) между записями и метриками.</summary>
public class GetCorrelationsQuery : IRequest<List<CorrelationResult>>
{
    public List<RecordInput> Records { get; init; } = new();
    public List<MetricInput> Metrics { get; init; } = new();
}
