namespace Client.Application.Commands;
using Microsoft.Extensions.Configuration;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using XDiary.Contracts;

public sealed class RegisterClientHandler : IRequestHandler<RegisterClientCommand, AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    private readonly string? _testOtp;
    public RegisterClientHandler(IRepository<IClients, IClientAnemicModel> repo, IConfiguration config) { _repo = repo; _testOtp = config["Clients:TestOtp"]; }
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(RegisterClientCommand r, CancellationToken ct)
    { var otp = _testOtp ?? Random.Shared.Next(100000, 1000000).ToString(); var res = Aggregate.CreateInstance(new AnemicModel()).Register(r.Phone, otp); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
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