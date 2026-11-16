namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Specifications;
using XDiary.Contracts;

/// <summary>
/// Агрегат Record: бизнес-методы по экранам визарда (классический CEQRS, без metadata-driven).
/// </summary>
public sealed class Aggregate : IRecordAggregate
{
    public IJournalAnemicModel Model { get; }

    private Aggregate(IJournalAnemicModel model) => Model = model;

    public static Aggregate CreateInstance(IJournalAnemicModel model) => new(model);

    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    public IRecordRoot Root => Model.Root;
    public Guid? ClientId => Model.ClientId;
    public int DurationMin => Model.DurationMin;
    public string Place => Model.Place;
    public IReadOnlyList<string> Positions => Model.Positions;
    public IReadOnlyList<string> Costumes => Model.Costumes;
    public IReadOnlyList<string> Toys => Model.Toys;
    public IReadOnlyList<string> Fantasies => Model.Fantasies;
    public IReadOnlyList<string> Tags => Model.Tags;
    public IReadOnlyList<string> Contraceptives => Model.Contraceptives;
    public int ContraceptiveCount => Model.ContraceptiveCount;
    public IReadOnlyList<IOrgasms> Orgasms => Model.Orgasms;
    public bool Garmin => Model.Garmin;
    public string GarminId => Model.GarminId;
    public string CreatedAt => Model.CreatedAt;
    public string UpdatedAt => Model.UpdatedAt;
    public RecordState State => Model.State;
    public bool Deleted => Model.Deleted;

    private AggregateResult<IJournal, IJournalAnemicModel> Success(AnemicModel newModel, [System.Runtime.CompilerServices.CallerMemberName] string commandName = "", params IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] specs)
    {
        newModel = newModel with { CommandName = commandName }; // канон §7: событие соразмерно методу
        return BusinessOperationData<IJournal, IJournalAnemicModel>.Commit<IJournal, IJournalAnemicModel>(Model, newModel)
            .ValidateCommand(specs);
    }

    private AggregateResult<IJournal, IJournalAnemicModel> Fail(string error)
    {
        var data = BusinessOperationData<IJournal, IJournalAnemicModel>.Commit<IJournal, IJournalAnemicModel>(Model, Model);
        return new AggregateResultException<IJournal, IJournalAnemicModel>(
            data, new FailedSpecification<IJournal, IJournalAnemicModel>(error));
    }

    private AnemicModel Clone() => new AnemicModel
    {
        Id = Model.Id,
        Version = Model.Version,
        CorrelationToken = Model.CorrelationToken,
        ClientId = Model.ClientId,
        Root = Model.Root,
        DurationMin = Model.DurationMin,
        Place = Model.Place,
        Positions = Model.Positions,
        Costumes = Model.Costumes,
        Toys = Model.Toys,
        Fantasies = Model.Fantasies,
        Tags = Model.Tags,
        Contraceptives = Model.Contraceptives,
        ContraceptiveCount = Model.ContraceptiveCount,
        Orgasms = Model.Orgasms,
        Garmin = Model.Garmin,
        GarminId = Model.GarminId,
        CreatedAt = Model.CreatedAt,
        UpdatedAt = Model.UpdatedAt,
        State = Model.State,
        Deleted = Model.Deleted
    };

    public AggregateResult<IJournal, IJournalAnemicModel> StartManualEntry(Guid clientId)
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        var anemic = new AnemicModel
        {
            Id = Model.Id, // @TL 22:21 — id агрегата стабилен: проекция/чтение ищут по нему, а не по id события
            Root = RecordRoot.CreateInstance(Array.Empty<string>(), default),
            ClientId = clientId,
            Garmin = false,
            State = RecordState.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> StartFromGarmin(string startedAt, int durationMin, string garminId, Guid clientId)
    {
        // TE-101: startedAt обязан быть ISO 8601; храним нормализованный ISO.
        var whenValidator = new WhenIsoValidator();
        if (!whenValidator.TryParse(startedAt, out var normalizedWhen)) return Fail(whenValidator.Reason);
        var now = DateTimeOffset.UtcNow.ToString("o");
        var anemic = new AnemicModel
        {
            Id = Model.Id, // @TL 22:21 — id агрегата стабилен
            Root = RecordRoot.CreateInstance(Array.Empty<string>(), normalizedWhen),
            ClientId = clientId,
            DurationMin = durationMin,
            Garmin = true,
            GarminId = garminId,
            State = RecordState.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> SpecifyWhen(string startedAt)
    {
        // TE-101: startedAt обязан быть ISO 8601; храним нормализованный ISO (не дословную строку).
        var v = new WhenIsoValidator();
        if (!v.TryParse(startedAt, out var normalized)) return Fail(v.Reason);
        var anemic = Clone() with { Root = RecordRoot.CreateInstance(Model.Root.Partners, normalized) };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> AddPartner(string partnerId)
    {
        var partners = new List<string>(Model.Root.Partners);
        if (!partners.Contains(partnerId))
            partners.Add(partnerId);

        var anemic = Clone() with { Root = RecordRoot.CreateInstance(partners, Model.Root.StartedAt) };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> RemovePartner(string partnerId)
    {
        var partners = Model.Root.Partners.Where(p => p != partnerId).ToList();
        var anemic = Clone() with { Root = RecordRoot.CreateInstance(partners, Model.Root.StartedAt) };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> SpecifyPlace(string place)
    {
        var anemic = Clone() with { Place = place };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> AddPositions(IEnumerable<string> positions)
    {
        var anemic = Clone() with { Positions = positions.ToList() };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> AddAccessories(
        IEnumerable<string> costumes, IEnumerable<string> toys, IEnumerable<string> fantasies, IEnumerable<string> tags)
    {
        var anemic = Clone() with { Costumes = costumes.ToList(), Toys = toys.ToList(), Fantasies = fantasies.ToList(), Tags = tags.ToList() };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> RecordContraception(IEnumerable<string> contraceptives, int count)
    {
        var anemic = Clone() with { Contraceptives = contraceptives.ToList(), ContraceptiveCount = count };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> SpecifyDuration(int durationMin)
    {
        var anemic = Clone() with { DurationMin = durationMin };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator(), new DurationPositiveValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> RecordOrgasms(IEnumerable<IOrgasms> orgasms)
    {
        var list = orgasms.ToList();
        var anemic = Clone() with { Orgasms = list };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator(), new RatingRangeValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> Complete()
    {
        var anemic = Clone() with { State = RecordState.Recorded, UpdatedAt = DateTimeOffset.UtcNow.ToString("o") };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordIsDraftValidator(), new WhenRequiredValidator() });
    }

    public AggregateResult<IJournal, IJournalAnemicModel> Delete()
    {
        var anemic = Clone() with { State = RecordState.Deleted, Deleted = true };
        return Success(anemic, specs: new IBusinessOperationSpecification<IJournal, IJournalAnemicModel>[] { new RecordNotDeletedValidator(), new RecordDeletableValidator() });
    }
}
