using AppleHealthGateway.InternalContracts;
using MediatR;

namespace AppleHealthGateway.Application.Queries;

/// <summary>Запрос: состояние импорта Apple Health по источнику.</summary>
public class GetImportStateQuery : IRequest<AppleImportReadModel?>
{
    public string SourceId { get; init; } = "apple-health";
}
