using Portability.InternalContracts;
using MediatR;

namespace Portability.Application.Queries;

/// <summary>Запрос: CSV-документ по типу источника.</summary>
public class GetDocumentQuery : IRequest<CsvDocumentReadModel?>
{
    public string SourceId { get; init; } = "records";
}
