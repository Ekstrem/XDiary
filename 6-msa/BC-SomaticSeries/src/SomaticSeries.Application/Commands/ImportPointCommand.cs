using MediatR;

namespace SomaticSeries.Application.Commands;

/// <summary>Команда: импортировать минутную точку соматики (upsert по t).</summary>
public class ImportPointCommand : IRequest<SomaticSeriesOperationResult>
{
    public string T { get; set; } = string.Empty;
    public int Hr { get; set; }
    public int BodyBattery { get; set; }
}
