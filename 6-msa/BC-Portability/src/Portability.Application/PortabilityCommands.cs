namespace Portability.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Portability.Domain;
using Portability.Domain.Abstraction;
public sealed record ExportRecordsCommand(IReadOnlyList<string> Rows) : IRequest<AggregateResult<IPortability, ICsvDocumentAnemicModel>>;
public sealed record ImportRecordsCommand(IReadOnlyList<string> Rows) : IRequest<AggregateResult<IPortability, ICsvDocumentAnemicModel>>;
public sealed record ImportSomaticCommand(IReadOnlyList<string> Rows) : IRequest<AggregateResult<IPortability, ICsvDocumentAnemicModel>>;