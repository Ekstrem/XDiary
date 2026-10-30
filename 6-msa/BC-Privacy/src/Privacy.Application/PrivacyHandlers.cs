namespace Privacy.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Privacy.Application.Security;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;
using Privacy.Domain.Specifications;
using Privacy.InternalContracts;
using XDiary.Contracts;

public sealed class SetPinHandler : IRequestHandler<SetPinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>
{
    private readonly IPinCredentialRepository _repo;
    public SetPinHandler(IPinCredentialRepository repo) => _repo = repo;
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(SetPinCommand r, CancellationToken ct)
    {
        var v = new PinFormatValidator(); if (!v.IsSatisfiedBy(r.Pin)) return PrivacyResults.Fail(v.Reason);
        var (salt, hash, iter) = PinHasher.Hash(r.Pin);
        var m = await _repo.GetBySourceIdAsync("pin", ct) ?? new AnemicModel();
        var res = Aggregate.CreateInstance(m).SetPin(salt, hash, iter);
        if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res;
    }
}
public sealed class ChangePinHandler : IRequestHandler<ChangePinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>
{
    private readonly IPinCredentialRepository _repo;
    public ChangePinHandler(IPinCredentialRepository repo) => _repo = repo;
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(ChangePinCommand r, CancellationToken ct)
    {
        var v = new PinFormatValidator(); if (!v.IsSatisfiedBy(r.Pin)) return PrivacyResults.Fail(v.Reason);
        var (salt, hash, iter) = PinHasher.Hash(r.Pin);
        var m = await _repo.GetBySourceIdAsync("pin", ct) ?? new AnemicModel();
        var res = Aggregate.CreateInstance(m).ChangePin(salt, hash, iter);
        if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res;
    }
}
public sealed class RemovePinHandler : IRequestHandler<RemovePinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>
{
    private readonly IPinCredentialRepository _repo;
    public RemovePinHandler(IPinCredentialRepository repo) => _repo = repo;
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(RemovePinCommand r, CancellationToken ct)
    { var m = await _repo.GetBySourceIdAsync("pin", ct) ?? new AnemicModel(); var res = Aggregate.CreateInstance(m).Remove(); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class LockPinHandler : IRequestHandler<LockPinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>
{
    private readonly IPinCredentialRepository _repo;
    public LockPinHandler(IPinCredentialRepository repo) => _repo = repo;
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(LockPinCommand r, CancellationToken ct)
    { var m = await _repo.GetBySourceIdAsync("pin", ct) ?? new AnemicModel(); var res = Aggregate.CreateInstance(m).Lock(); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}
public sealed class UnlockPinHandler : IRequestHandler<UnlockPinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>
{
    private readonly IPinCredentialRepository _repo;
    public UnlockPinHandler(IPinCredentialRepository repo) => _repo = repo;
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(UnlockPinCommand r, CancellationToken ct)
    { var m = await _repo.GetBySourceIdAsync("pin", ct) ?? new AnemicModel(); var res = Aggregate.CreateInstance(m).Unlock(); if (res.IsSuccess()) await _repo.SaveAsync(res, ct); return res; }
}