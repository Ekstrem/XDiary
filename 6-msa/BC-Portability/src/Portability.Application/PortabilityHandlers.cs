namespace Portability.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;
using Portability.InternalContracts;
using XDiary.Contracts;

public sealed class ExportRecordsHandler : IRequestHandler<ExportRecordsCommand, AggregateResult<IPortability, ICsvDocumentAnemicModel>>
{
    private readonly IPortabilityRepository _repo;
    public ExportRecordsHandler(IPortabilityRepository repo) => _repo = repo;
    public async Task<AggregateResult<IPortability, ICsvDocumentAnemicModel>> Handle(ExportRecordsCommand r, CancellationToken ct)
    { var res = Aggregate.CreateInstance(new AnemicModel()).ExportRecords(r.Rows); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class ImportRecordsHandler : IRequestHandler<ImportRecordsCommand, AggregateResult<IPortability, ICsvDocumentAnemicModel>>
{
    private readonly IPortabilityRepository _repo;
    public ImportRecordsHandler(IPortabilityRepository repo) => _repo = repo;
    public async Task<AggregateResult<IPortability, ICsvDocumentAnemicModel>> Handle(ImportRecordsCommand r, CancellationToken ct)
    { var res = Aggregate.CreateInstance(new AnemicModel()).ImportRecords(r.Rows); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class ImportSomaticHandler : IRequestHandler<ImportSomaticCommand, AggregateResult<IPortability, ICsvDocumentAnemicModel>>
{
    private readonly IPortabilityRepository _repo;
    public ImportSomaticHandler(IPortabilityRepository repo) => _repo = repo;
    public async Task<AggregateResult<IPortability, ICsvDocumentAnemicModel>> Handle(ImportSomaticCommand r, CancellationToken ct)
    { var res = Aggregate.CreateInstance(new AnemicModel()).ImportSomatic(r.Rows); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}