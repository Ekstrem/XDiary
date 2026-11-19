namespace Portability.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Portability.Domain;
using Portability.Domain.Abstraction;
public sealed record ExportRecordsCommand(IReadOnlyList<string> Rows) : IRequest<AggregateResult<IPortability, ICsvDocumentAnemicModel>>;
public sealed record ImportRecordsCommand(Guid Id, IReadOnlyList<string> Rows) : IRequest<AggregateResult<IPortability, ICsvDocumentAnemicModel>>;
public sealed record ImportSomaticCommand(Guid Id, IReadOnlyList<string> Rows) : IRequest<AggregateResult<IPortability, ICsvDocumentAnemicModel>>;