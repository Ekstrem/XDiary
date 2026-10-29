using MediatR;

namespace Portability.Application.Commands;

/// <summary>Команда: сохранить CSV-документ (upsert по sourceId).</summary>
public class UpsertDocumentCommand : IRequest<PortabilityOperationResult>
{
    public string SourceId { get; set; } = "records";
    public List<string> Headers { get; set; } = new();
    public List<string> Rows { get; set; } = new();
}
