namespace Privacy.Application.Commands;
using Privacy.Application;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using Privacy.Application.Security;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Specifications;
using Privacy.InternalContracts;
using XDiary.Contracts;

public sealed class SetPinHandler : IRequestHandler<SetPinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPrivacy, IPinCredentialAggregate> _provider;
    private readonly IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IPrivacy, PinCredentialReadModel> _read;
    public SetPinHandler(IAggregateProvider<IPrivacy, IPinCredentialAggregate> provider, IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> observer, IActorProvider actor, IReadRepository<IPrivacy, PinCredentialReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(SetPinCommand r, CancellationToken ct)
    {
        var v = new PinFormatSpecification(); if (!v.IsSatisfiedBy(r.Pin)) return PrivacyResults.Fail(v.Reason);
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "pin");
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IPrivacy, IPinCredentialAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is not null && !string.IsNullOrEmpty(aggregate.Root.Hash))
            if (string.IsNullOrWhiteSpace(r.CurrentPin) || !PinHasher.Verify(r.CurrentPin, aggregate.Root.Salt, aggregate.Root.Hash, aggregate.Root.Iterations))
                return PrivacyResults.Fail("Текущий PIN неверен");
        var (salt, hash, iter) = PinHasher.Hash(r.Pin);
        return aggregate.SetPin(_actor.ClientId, salt, hash, iter);
    }
}
public sealed class ChangePinHandler : IRequestHandler<ChangePinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPrivacy, IPinCredentialAggregate> _provider;
    private readonly IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IPrivacy, PinCredentialReadModel> _read;
    public ChangePinHandler(IAggregateProvider<IPrivacy, IPinCredentialAggregate> provider, IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> observer, IActorProvider actor, IReadRepository<IPrivacy, PinCredentialReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(ChangePinCommand r, CancellationToken ct)
    {
        var v = new PinFormatSpecification(); if (!v.IsSatisfiedBy(r.Pin)) return PrivacyResults.Fail(v.Reason);
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "pin");
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IPrivacy, IPinCredentialAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is not null && !string.IsNullOrEmpty(aggregate.Root.Hash))
            if (string.IsNullOrWhiteSpace(r.CurrentPin) || !PinHasher.Verify(r.CurrentPin, aggregate.Root.Salt, aggregate.Root.Hash, aggregate.Root.Iterations))
                return PrivacyResults.Fail("Текущий PIN неверен");
        var (salt, hash, iter) = PinHasher.Hash(r.Pin);
        return aggregate.ChangePin(salt, hash, iter);
    }
}
public sealed class RemovePinHandler : IRequestHandler<RemovePinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPrivacy, IPinCredentialAggregate> _provider;
    private readonly IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IPrivacy, PinCredentialReadModel> _read;
    public RemovePinHandler(IAggregateProvider<IPrivacy, IPinCredentialAggregate> provider, IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> observer, IActorProvider actor, IReadRepository<IPrivacy, PinCredentialReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(RemovePinCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "pin");
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IPrivacy, IPinCredentialAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is not null && !string.IsNullOrEmpty(aggregate.Root.Hash))
            if (string.IsNullOrWhiteSpace(r.CurrentPin) || !PinHasher.Verify(r.CurrentPin, aggregate.Root.Salt, aggregate.Root.Hash, aggregate.Root.Iterations))
                return PrivacyResults.Fail("Текущий PIN неверен");
        return aggregate.Remove();
    }
}
public sealed class LockPinHandler : IRequestHandler<LockPinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPrivacy, IPinCredentialAggregate> _provider;
    private readonly IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IPrivacy, PinCredentialReadModel> _read;
    public LockPinHandler(IAggregateProvider<IPrivacy, IPinCredentialAggregate> provider, IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> observer, IActorProvider actor, IReadRepository<IPrivacy, PinCredentialReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(LockPinCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "pin");
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IPrivacy, IPinCredentialAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.Lock();
    }
}
public sealed class UnlockPinHandler : IRequestHandler<UnlockPinCommand, AggregateResult<IPrivacy, IPinCredentialAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPrivacy, IPinCredentialAggregate> _provider;
    private readonly IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IPrivacy, PinCredentialReadModel> _read;
    public UnlockPinHandler(IAggregateProvider<IPrivacy, IPinCredentialAggregate> provider, IObserver<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> observer, IActorProvider actor, IReadRepository<IPrivacy, PinCredentialReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IPrivacy, IPinCredentialAnemicModel>> Handle(UnlockPinCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "pin");
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IPrivacy, IPinCredentialAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.Unlock();
    }
}
