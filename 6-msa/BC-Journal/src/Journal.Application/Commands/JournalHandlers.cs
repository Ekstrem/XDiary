namespace Journal.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Application;
using Journal.Domain.Implementation;
using Journal.InternalContracts;
using MediatR;
using XDiary.Contracts;

public sealed class StartManualEntryHandler : IRequestHandler<StartManualEntryCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    public StartManualEntryHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer, IActorProvider actor) { _provider = provider; _observer = observer; _actor = actor; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(StartManualEntryCommand r, CancellationToken ct)
    {
        var id = r.Id == Guid.Empty ? Guid.NewGuid() : r.Id;
        var aggregate = await _provider.GetAggregateAsync(id, ct);
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.StartManualEntry(_actor.ClientId);
    }
}
public sealed class StartFromGarminHandler : IRequestHandler<StartFromGarminCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    private readonly IJournalQueryRepository _query;
    private readonly IActorProvider _actor;
    public StartFromGarminHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer, IJournalQueryRepository query, IActorProvider actor) { _provider = provider; _observer = observer; _query = query; _actor = actor; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(StartFromGarminCommand r, CancellationToken ct)
    {
        // BE-051: повторный синк той же garmin-активности → та же запись (dedup по garminId).
        var existing = await _query.GetByGarminIdAsync(r.GarminId, ct);
        var id = existing?.Id ?? Guid.NewGuid();
        var aggregate = await _provider.GetAggregateAsync(id, ct);
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.StartFromGarmin(r.StartedAt, r.DurationMin, r.GarminId, _actor.ClientId);
    }
}
public sealed class SpecifyWhenHandler : IRequestHandler<SpecifyWhenCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public SpecifyWhenHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(SpecifyWhenCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.SpecifyWhen(r.StartedAt);
    }
}
public sealed class AddPartnerHandler : IRequestHandler<AddPartnerCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public AddPartnerHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(AddPartnerCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.AddPartner(r.PartnerId);
    }
}
public sealed class RemovePartnerHandler : IRequestHandler<RemovePartnerCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public RemovePartnerHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(RemovePartnerCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.RemovePartner(r.PartnerId);
    }
}
public sealed class SpecifyPlaceHandler : IRequestHandler<SpecifyPlaceCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public SpecifyPlaceHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(SpecifyPlaceCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.SpecifyPlace(r.Place);
    }
}
public sealed class AddPositionsHandler : IRequestHandler<AddPositionsCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public AddPositionsHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(AddPositionsCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.AddPositions(r.Positions);
    }
}
public sealed class AddAccessoriesHandler : IRequestHandler<AddAccessoriesCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public AddAccessoriesHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(AddAccessoriesCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.AddAccessories(r.Costumes, r.Toys, r.Fantasies, r.Tags);
    }
}
public sealed class RecordContraceptionHandler : IRequestHandler<RecordContraceptionCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public RecordContraceptionHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(RecordContraceptionCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.RecordContraception(r.Contraceptives, r.ContraceptiveCount);
    }
}
public sealed class SpecifyDurationHandler : IRequestHandler<SpecifyDurationCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public SpecifyDurationHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(SpecifyDurationCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.SpecifyDuration(r.DurationMin);
    }
}
public sealed class RecordOrgasmsHandler : IRequestHandler<RecordOrgasmsCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    public RecordOrgasmsHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(RecordOrgasmsCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        var orgasms = r.Orgasms.Select(o => Orgasms.CreateInstance(o.Participant, o.Count, o.Types, o.Rating)).ToList();
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.RecordOrgasms(orgasms);
    }
}
public sealed class CompleteHandler : IRequestHandler<CompleteCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    private readonly IJournalEventPublisher _publisher;
    public CompleteHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer, IJournalEventPublisher publisher) { _provider = provider; _observer = observer; _publisher = publisher; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(CompleteCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var result = aggregate.Complete();
        if (result.IsSuccess()) await _publisher.PublishRecordCompletedAsync(result.Model(), ct);
        return result;
    }
}
public sealed class DeleteRecordHandler : IRequestHandler<DeleteRecordCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IAggregateProvider<IJournal, IRecordAggregate> _provider;
    private readonly IObserver<AggregateResult<IJournal, IJournalAnemicModel>> _observer;
    private readonly IJournalEventPublisher _publisher;
    public DeleteRecordHandler(IAggregateProvider<IJournal, IRecordAggregate> provider, IObserver<AggregateResult<IJournal, IJournalAnemicModel>> observer, IJournalEventPublisher publisher) { _provider = provider; _observer = observer; _publisher = publisher; }
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(DeleteRecordCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        if (aggregate.Root is null) return JournalResults.Fail("Record not found.");
        ((IObservable<AggregateResult<IJournal, IJournalAnemicModel>>)aggregate).Subscribe(_observer);
        var result = aggregate.Delete();
        if (result.IsSuccess()) await _publisher.PublishRecordDeletedAsync(result.Model(), ct);
        return result;
    }
}
