namespace Client.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using XDiary.Contracts;

public sealed class RegisterClientHandler : IRequestHandler<RegisterClientCommand, AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    public RegisterClientHandler(IRepository<IClients, IClientAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(RegisterClientCommand r, CancellationToken ct)
    { var res = Aggregate.CreateInstance(new AnemicModel()).Register(r.Phone); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class VerifyClientOtpHandler : IRequestHandler<VerifyClientOtpCommand, AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    public VerifyClientOtpHandler(IRepository<IClients, IClientAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(VerifyClientOtpCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return ClientResults.Fail("Client not found."); var res = Aggregate.CreateInstance(m).VerifyOtp(r.Code); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class LinkClientPartnerHandler : IRequestHandler<LinkClientPartnerCommand, AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    public LinkClientPartnerHandler(IRepository<IClients, IClientAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(LinkClientPartnerCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return ClientResults.Fail("Client not found."); var res = Aggregate.CreateInstance(m).LinkPartner(r.PartnerId); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class UnlinkClientPartnerHandler : IRequestHandler<UnlinkClientPartnerCommand, AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    public UnlinkClientPartnerHandler(IRepository<IClients, IClientAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(UnlinkClientPartnerCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return ClientResults.Fail("Client not found."); var res = Aggregate.CreateInstance(m).UnlinkPartner(); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}