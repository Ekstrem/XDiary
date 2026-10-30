namespace Partners.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using XDiary.Contracts;

public sealed class RegisterPartnerHandler : IRequestHandler<RegisterPartnerCommand, AggregateResult<IPartners, IPartnerAnemicModel>>
{
    private readonly IRepository<IPartners, IPartnerAnemicModel> _repo;
    public RegisterPartnerHandler(IRepository<IPartners, IPartnerAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(RegisterPartnerCommand r, CancellationToken ct)
    { var res = Aggregate.CreateInstance(new AnemicModel()).Register(r.Name); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class RenamePartnerHandler : IRequestHandler<RenamePartnerCommand, AggregateResult<IPartners, IPartnerAnemicModel>>
{
    private readonly IRepository<IPartners, IPartnerAnemicModel> _repo;
    public RenamePartnerHandler(IRepository<IPartners, IPartnerAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(RenamePartnerCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return PartnersResults.Fail("Partner not found."); var res = Aggregate.CreateInstance(m).Rename(r.Name); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class UpdatePartnerProfileHandler : IRequestHandler<UpdatePartnerProfileCommand, AggregateResult<IPartners, IPartnerAnemicModel>>
{
    private readonly IRepository<IPartners, IPartnerAnemicModel> _repo;
    public UpdatePartnerProfileHandler(IRepository<IPartners, IPartnerAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(UpdatePartnerProfileCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return PartnersResults.Fail("Partner not found."); var res = Aggregate.CreateInstance(m).UpdateProfile(r.Photo, r.Gender, r.Notes); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class RemovePartnerHandler : IRequestHandler<RemovePartnerCommand, AggregateResult<IPartners, IPartnerAnemicModel>>
{
    private readonly IRepository<IPartners, IPartnerAnemicModel> _repo;
    public RemovePartnerHandler(IRepository<IPartners, IPartnerAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(RemovePartnerCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return PartnersResults.Fail("Partner not found."); var res = Aggregate.CreateInstance(m).Remove(); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}