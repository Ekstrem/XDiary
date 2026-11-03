namespace Client.Application.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.Application.Abstraction;
using Client.InternalContracts;
using XDiary.Contracts;

public sealed class RegisterClientHandler : IRequestHandler<RegisterClientCommand, AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    private readonly IClientQueryRepository _query;
    private readonly IOtpSender _sender;
    private readonly string? _testOtp;
    public RegisterClientHandler(IRepository<IClients, IClientAnemicModel> repo, IClientQueryRepository query, IOtpSender sender, IConfiguration config, Microsoft.Extensions.Logging.ILogger<RegisterClientHandler> logger) { _repo = repo; _query = query; _sender = sender; _testOtp = TestOtpPolicy.Resolve(config, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), out var warn); if (warn) logger.LogWarning("Включён тестовый OTP на проде (Clients:AllowTestOtpOutsideDev=true) — временная мера"); }
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(RegisterClientCommand r, CancellationToken ct)
    { var otp = _testOtp ?? Random.Shared.Next(100000, 1000000).ToString(); if (_testOtp is null) { try { await _sender.SendAsync(r.Phone, otp, ct); } catch (Exception ex) { return ClientResults.Fail(ex.Message); } } var existing = await _query.GetByPhoneAsync(r.Phone, ct); if (existing is not null) { var agg = await _repo.GetByIdAsync(existing.Id, ct); if (agg is not null) { var rr = Aggregate.CreateInstance(agg).ReissueOtp(otp); if (rr.IsSuccess()) { try { await _repo.SaveAsync(rr, ct); } catch (DuplicatePhoneException ex) { return ClientResults.Fail(ex.Message); } } return rr; } } var res = Aggregate.CreateInstance(new AnemicModel()).Register(r.Phone, otp); if (res.IsSuccess()) { try { await _repo.SaveAsync(res, ct); } catch (DuplicatePhoneException ex) { return ClientResults.Fail(ex.Message); } } return res; }
}
public sealed class VerifyClientOtpHandler : IRequestHandler<VerifyClientOtpCommand, AggregateResult<IClients, IClientAnemicModel>>
{
    private readonly IRepository<IClients, IClientAnemicModel> _repo;
    public VerifyClientOtpHandler(IRepository<IClients, IClientAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IClients, IClientAnemicModel>> Handle(VerifyClientOtpCommand r, CancellationToken ct)
    { var m = await _repo.GetByIdAsync(r.AggregateId, ct); if (m is null) return ClientResults.Fail("Client not found."); var res = Aggregate.CreateInstance(m).VerifyOtp(r.Code); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); else { var fail = Aggregate.CreateInstance(m).RecordFailedAttempt(); await _repo.SaveAsync(fail, ct); } return res; }
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