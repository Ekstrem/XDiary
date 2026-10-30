namespace Journal.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using MediatR;
using XDiary.Contracts;

public sealed class StartManualEntryHandler : IRequestHandler<StartManualEntryCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public StartManualEntryHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(StartManualEntryCommand r, CancellationToken ct)
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry();
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class StartFromGarminHandler : IRequestHandler<StartFromGarminCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public StartFromGarminHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(StartFromGarminCommand r, CancellationToken ct)
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).StartFromGarmin(r.StartedAt, r.DurationMin, r.GarminId);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class SpecifyWhenHandler : IRequestHandler<SpecifyWhenCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public SpecifyWhenHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(SpecifyWhenCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).SpecifyWhen(r.StartedAt);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class AddPartnerHandler : IRequestHandler<AddPartnerCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public AddPartnerHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(AddPartnerCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).AddPartner(r.PartnerId);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class RemovePartnerHandler : IRequestHandler<RemovePartnerCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public RemovePartnerHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(RemovePartnerCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).RemovePartner(r.PartnerId);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class SpecifyPlaceHandler : IRequestHandler<SpecifyPlaceCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public SpecifyPlaceHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(SpecifyPlaceCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).SpecifyPlace(r.Place);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class AddPositionsHandler : IRequestHandler<AddPositionsCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public AddPositionsHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(AddPositionsCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).AddPositions(r.Positions);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class AddAccessoriesHandler : IRequestHandler<AddAccessoriesCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public AddAccessoriesHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(AddAccessoriesCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).AddAccessories(r.Costumes, r.Toys, r.Fantasies, r.Tags);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class RecordContraceptionHandler : IRequestHandler<RecordContraceptionCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public RecordContraceptionHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(RecordContraceptionCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).RecordContraception(r.Contraceptives, r.ContraceptiveCount);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class SpecifyDurationHandler : IRequestHandler<SpecifyDurationCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public SpecifyDurationHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(SpecifyDurationCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).SpecifyDuration(r.DurationMin);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class RecordOrgasmsHandler : IRequestHandler<RecordOrgasmsCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public RecordOrgasmsHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(RecordOrgasmsCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var orgasms = r.Orgasms.Select(o => Orgasms.CreateInstance(o.Participant, o.Count, o.Types, o.Rating)).ToList();
        var result = Aggregate.CreateInstance(model).RecordOrgasms(orgasms);
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class CompleteHandler : IRequestHandler<CompleteCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public CompleteHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(CompleteCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).Complete();
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}
public sealed class DeleteRecordHandler : IRequestHandler<DeleteRecordCommand, AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IRepository<IJournal, IJournalAnemicModel> _repo;
    public DeleteRecordHandler(IRepository<IJournal, IJournalAnemicModel> repo) => _repo = repo;
    public async Task<AggregateResult<IJournal, IJournalAnemicModel>> Handle(DeleteRecordCommand r, CancellationToken ct)
    {
        var model = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (model is null) return JournalResults.Fail("Record not found.");
        var result = Aggregate.CreateInstance(model).Delete();
        if (result.IsSuccess()) await _repo.SaveAsync(result, ct);
        return result;
    }
}