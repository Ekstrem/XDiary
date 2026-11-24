namespace Partners.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using MediatR;
using Partners.Domain;
using Partners.Domain.Abstraction;
using XDiary.Contracts;

public sealed class RegisterPartnerHandler : IRequestHandler<RegisterPartnerCommand, AggregateResult<IPartners, IPartnerAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPartners, IPartnerAggregate> _provider;
    private readonly IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    public RegisterPartnerHandler(IAggregateProvider<IPartners, IPartnerAggregate> provider, IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> observer, IActorProvider actor) { _provider = provider; _observer = observer; _actor = actor; }
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(RegisterPartnerCommand r, CancellationToken ct)
    {
        var id = r.Id == Guid.Empty ? Guid.NewGuid() : r.Id;
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IPartners, IPartnerAggregate>)_provider).GetAggregateWithCompletionAsync(id, ct);
        ((IObservable<AggregateResult<IPartners, IPartnerAnemicModel>>)aggregate).Subscribe(_observer);
        var result = aggregate.Register(r.Name, _actor.ClientId, AggregateCommandFactory.Create("Register"));
        await completion;
        return result;
    }
}
public sealed class RenamePartnerHandler : IRequestHandler<RenamePartnerCommand, AggregateResult<IPartners, IPartnerAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPartners, IPartnerAggregate> _provider;
    private readonly IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> _observer;
    public RenamePartnerHandler(IAggregateProvider<IPartners, IPartnerAggregate> provider, IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(RenamePartnerCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IPartners, IPartnerAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<IPartners, IPartnerAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return PartnersResults.Fail("Partner not found.");
        var result = aggregate.Rename(r.Name, AggregateCommandFactory.Create("Rename"));
        await completion;
        return result;
    }
}
public sealed class UpdatePartnerProfileHandler : IRequestHandler<UpdatePartnerProfileCommand, AggregateResult<IPartners, IPartnerAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPartners, IPartnerAggregate> _provider;
    private readonly IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> _observer;
    public UpdatePartnerProfileHandler(IAggregateProvider<IPartners, IPartnerAggregate> provider, IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(UpdatePartnerProfileCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IPartners, IPartnerAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<IPartners, IPartnerAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return PartnersResults.Fail("Partner not found.");
        var result = aggregate.UpdateProfile(r.Photo, r.Gender, r.Notes, AggregateCommandFactory.Create("UpdateProfile"));
        await completion;
        return result;
    }
}
public sealed class RemovePartnerHandler : IRequestHandler<RemovePartnerCommand, AggregateResult<IPartners, IPartnerAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPartners, IPartnerAggregate> _provider;
    private readonly IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> _observer;
    public RemovePartnerHandler(IAggregateProvider<IPartners, IPartnerAggregate> provider, IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(RemovePartnerCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IPartners, IPartnerAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<IPartners, IPartnerAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return PartnersResults.Fail("Partner not found.");
        var result = aggregate.Remove(AggregateCommandFactory.Create("Remove"));
        await completion;
        return result;
    }
}
public sealed class LinkPartnerClientHandler : IRequestHandler<LinkPartnerClientCommand, AggregateResult<IPartners, IPartnerAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPartners, IPartnerAggregate> _provider;
    private readonly IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    public LinkPartnerClientHandler(IAggregateProvider<IPartners, IPartnerAggregate> provider, IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> observer, IActorProvider actor) { _provider = provider; _observer = observer; _actor = actor; }
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(LinkPartnerClientCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IPartners, IPartnerAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<IPartners, IPartnerAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return PartnersResults.Fail("Partner not found.");
        var result = aggregate.LinkClient(_actor.ClientId, AggregateCommandFactory.Create("LinkClient"));
        await completion;
        return result;
    }
}
public sealed class UnlinkPartnerClientHandler : IRequestHandler<UnlinkPartnerClientCommand, AggregateResult<IPartners, IPartnerAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IPartners, IPartnerAggregate> _provider;
    private readonly IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> _observer;
    public UnlinkPartnerClientHandler(IAggregateProvider<IPartners, IPartnerAggregate> provider, IObserver<AggregateResult<IPartners, IPartnerAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IPartners, IPartnerAnemicModel>> Handle(UnlinkPartnerClientCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IPartners, IPartnerAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<IPartners, IPartnerAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return PartnersResults.Fail("Partner not found.");
        var result = aggregate.UnlinkClient(AggregateCommandFactory.Create("UnlinkClient"));
        await completion;
        return result;
    }
}
