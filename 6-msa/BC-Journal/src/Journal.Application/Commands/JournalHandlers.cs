namespace Journal.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Application;
using Journal.Domain.Implementation;
using Journal.InternalContracts;
using MediatR;
using XDiary.Contracts;

public sealed class StartManualEntryHandler : IRequestHandler<StartManualEntryCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    public StartManualEntryHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer, IActorProvider actor) { _provider = provider; _observer = observer; _actor = actor; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(StartManualEntryCommand r, CancellationToken ct)
    {
        var id = r.Id == Guid.Empty ? Guid.NewGuid() : r.Id;
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(id, ct);
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.StartManualEntry), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.StartManualEntry(_actor.ClientId, command);
        await completion;
        return result;
    }
}
public sealed class StartFromGarminHandler : IRequestHandler<StartFromGarminCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    private readonly IReadRepository<IJournal, JournalReadModel> _read;
    private readonly IActorProvider _actor;
    public StartFromGarminHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer, IReadRepository<IJournal, JournalReadModel> read, IActorProvider actor) { _provider = provider; _observer = observer; _read = read; _actor = actor; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(StartFromGarminCommand r, CancellationToken ct)
    {
        // BE-051: РїРѕРІС‚РѕСЂРЅС‹Р№ СЃРёРЅРє С‚РѕР№ Р¶Рµ garmin-Р°РєС‚РёРІРЅРѕСЃС‚Рё в†’ С‚Р° Р¶Рµ Р·Р°РїРёСЃСЊ (dedup РїРѕ garminId).
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.GarminId == r.GarminId);
        var id = existing?.Id ?? Guid.NewGuid();
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(id, ct);
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.StartFromGarmin), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.StartFromGarmin(r.StartedAt, r.DurationMin, r.GarminId, _actor.ClientId, command);
        await completion;
        return result;
    }
}
public sealed class SpecifyWhenHandler : IRequestHandler<SpecifyWhenCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public SpecifyWhenHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(SpecifyWhenCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.SpecifyWhen), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.SpecifyWhen(r.StartedAt, command);
        await completion;
        return result;
    }
}
public sealed class AddPartnerHandler : IRequestHandler<AddPartnerCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public AddPartnerHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(AddPartnerCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.AddPartner), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.AddPartner(r.PartnerId, command);
        await completion;
        return result;
    }
}
public sealed class RemovePartnerHandler : IRequestHandler<RemovePartnerCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public RemovePartnerHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(RemovePartnerCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.RemovePartner), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.RemovePartner(r.PartnerId, command);
        await completion;
        return result;
    }
}
public sealed class SpecifyPlaceHandler : IRequestHandler<SpecifyPlaceCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public SpecifyPlaceHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(SpecifyPlaceCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.SpecifyPlace), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.SpecifyPlace(r.Place, command);
        await completion;
        return result;
    }
}
public sealed class AddPositionsHandler : IRequestHandler<AddPositionsCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public AddPositionsHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(AddPositionsCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.AddPositions), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.AddPositions(r.Positions, command);
        await completion;
        return result;
    }
}
public sealed class AddAccessoriesHandler : IRequestHandler<AddAccessoriesCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public AddAccessoriesHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(AddAccessoriesCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.AddAccessories), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.AddAccessories(r.Costumes, r.Toys, r.Fantasies, r.Tags, command);
        await completion;
        return result;
    }
}
public sealed class RecordContraceptionHandler : IRequestHandler<RecordContraceptionCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public RecordContraceptionHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(RecordContraceptionCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.RecordContraception), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.RecordContraception(r.Contraceptives, r.ContraceptiveCount, command);
        await completion;
        return result;
    }
}
public sealed class SpecifyDurationHandler : IRequestHandler<SpecifyDurationCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public SpecifyDurationHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(SpecifyDurationCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.SpecifyDuration), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.SpecifyDuration(r.DurationMin, command);
        await completion;
        return result;
    }
}
public sealed class RecordOrgasmsHandler : IRequestHandler<RecordOrgasmsCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public RecordOrgasmsHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(RecordOrgasmsCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        var orgasms = r.Orgasms.Select(o => Orgasms.CreateInstance(o.Participant, o.Count, o.Types, o.Rating)).ToList();
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.RecordOrgasms), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.RecordOrgasms(orgasms, command);
        await completion;
        return result;
    }
}
public sealed class CompleteHandler : IRequestHandler<CompleteCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    private readonly IJournalEventPublisher _publisher;
    public CompleteHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer, IJournalEventPublisher publisher) { _provider = provider; _observer = observer; _publisher = publisher; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(CompleteCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.Complete), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.Complete(command);
        await completion;
        if (result.IsSuccess()) await _publisher.PublishRecordCompletedAsync(result.Model(), ct);
        return result;
    }
}
public sealed class DeleteRecordHandler : IRequestHandler<DeleteRecordCommand, AggregateResult<IJournal, IJournalAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    private readonly IJournalEventPublisher _publisher;
    public DeleteRecordHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer, IJournalEventPublisher publisher) { _provider = provider; _observer = observer; _publisher = publisher; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(DeleteRecordCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IJournal, IRecordAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var command = CommandToAggregate.Commit(Guid.NewGuid(), nameof(IRecordAggregate.Delete), "Journal", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = aggregate.Delete(command);
        await completion;
        if (result.IsSuccess()) await _publisher.PublishRecordDeletedAsync(result.Model(), ct);
        return result;
    }
}
