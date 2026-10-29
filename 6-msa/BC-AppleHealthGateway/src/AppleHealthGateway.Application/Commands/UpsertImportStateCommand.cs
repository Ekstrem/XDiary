using MediatR;

namespace AppleHealthGateway.Application.Commands;

/// <summary>Команда: сохранить состояние импорта Apple Health (upsert по sourceId).</summary>
public class UpsertImportStateCommand : IRequest<AppleHealthGatewayOperationResult>
{
    public string SourceId { get; set; } = "apple-health";
    public List<string> Metrics { get; set; } = new();
    public string LastImport { get; set; } = string.Empty;
    public List<string> ProcessedFiles { get; set; } = new();
}
