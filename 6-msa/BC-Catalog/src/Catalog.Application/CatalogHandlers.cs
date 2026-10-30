namespace Catalog.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;
using XDiary.Contracts;

public sealed class AddCatalogItemHandler : IRequestHandler<AddCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>
{
    private readonly IRepository<ICatalog, ICatalogAnemicModel> _repo;
    public AddCatalogItemHandler(IRepository<ICatalog, ICatalogAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(AddCatalogItemCommand r, CancellationToken ct)
    { var res = Aggregate.CreateInstance(new AnemicModel()).AddItem(r.Kind, r.Name); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class RenameCatalogItemHandler : IRequestHandler<RenameCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>
{
    private readonly IRepository<ICatalog, ICatalogAnemicModel> _repo;
    public RenameCatalogItemHandler(IRepository<ICatalog, ICatalogAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(RenameCatalogItemCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return CatalogResults.Fail("Catalog item not found."); var res = Aggregate.CreateInstance(m).Rename(r.Name); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class UpdateCatalogItemDetailsHandler : IRequestHandler<UpdateCatalogItemDetailsCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>
{
    private readonly IRepository<ICatalog, ICatalogAnemicModel> _repo;
    public UpdateCatalogItemDetailsHandler(IRepository<ICatalog, ICatalogAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(UpdateCatalogItemDetailsCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return CatalogResults.Fail("Catalog item not found."); var res = Aggregate.CreateInstance(m).UpdateDetails(r.Photo, r.Notes, r.Brand, r.FantasyPartner); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class ArchiveCatalogItemHandler : IRequestHandler<ArchiveCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>
{
    private readonly IRepository<ICatalog, ICatalogAnemicModel> _repo;
    public ArchiveCatalogItemHandler(IRepository<ICatalog, ICatalogAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(ArchiveCatalogItemCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return CatalogResults.Fail("Catalog item not found."); var res = Aggregate.CreateInstance(m).Archive(); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class RestoreCatalogItemHandler : IRequestHandler<RestoreCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>
{
    private readonly IRepository<ICatalog, ICatalogAnemicModel> _repo;
    public RestoreCatalogItemHandler(IRepository<ICatalog, ICatalogAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(RestoreCatalogItemCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return CatalogResults.Fail("Catalog item not found."); var res = Aggregate.CreateInstance(m).Restore(); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class RemoveCatalogItemHandler : IRequestHandler<RemoveCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>
{
    private readonly IRepository<ICatalog, ICatalogAnemicModel> _repo;
    public RemoveCatalogItemHandler(IRepository<ICatalog, ICatalogAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(RemoveCatalogItemCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return CatalogResults.Fail("Catalog item not found."); var res = Aggregate.CreateInstance(m).Remove(); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}