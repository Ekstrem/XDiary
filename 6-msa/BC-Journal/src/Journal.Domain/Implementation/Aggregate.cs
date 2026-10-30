namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Specifications;
using XDiary.Contracts;

/// <summary>
/// Агрегат Record: бизнес-методы по экранам визарда (классический CEQRS, без metadata-driven).
/// </summary>
public sealed class Aggregate
{
    public IJournalAnemicModel Model { get; }

    private Aggregate(IJournalAnemicModel model) => Model = model;

    public static Aggregate CreateInstance(IJournalAnemicModel model) => new(model);

    private AggregateResult<IJournal, IJournalAnemicModel> Success(AnemicModel newModel)
    {
        var data = BusinessOperationData<IJournal, IJournalAnemicModel>.Commit<IJournal, IJournalAnemicModel>(Model, newModel);
        return new AggregateResultSuccess<IJournal, IJournalAnemicModel>(data);
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
        UpdatedAt = Model.UpdatedAt
    };

    public AggregateResult<IJournal, IJournalAnemicModel> StartManualEntry()
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        var anemic = new AnemicModel
        {
            Root = RecordRoot.CreateInstance(Array.Empty<string>(), string.Empty),
            Garmin = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> StartFromGarmin(string startedAt, int durationMin, string garminId)
    {
        var now = DateTimeOffset.UtcNow.ToString("o");
        var anemic = new AnemicModel
        {
            Root = RecordRoot.CreateInstance(Array.Empty<string>(), startedAt),
            DurationMin = durationMin,
            Garmin = true,
            GarminId = garminId,
            CreatedAt = now,
            UpdatedAt = now
        };
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> SpecifyWhen(string startedAt)
    {
        var anemic = Clone();
        anemic.Root = RecordRoot.CreateInstance(Model.Root.Partners, startedAt);
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> AddPartner(string partnerId)
    {
        var partners = new List<string>(Model.Root.Partners);
        if (!partners.Contains(partnerId))
            partners.Add(partnerId);

        var anemic = Clone();
        anemic.Root = RecordRoot.CreateInstance(partners, Model.Root.StartedAt);
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> RemovePartner(string partnerId)
    {
        var partners = Model.Root.Partners.Where(p => p != partnerId).ToList();
        var anemic = Clone();
        anemic.Root = RecordRoot.CreateInstance(partners, Model.Root.StartedAt);
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> SpecifyPlace(string place)
    {
        var anemic = Clone();
        anemic.Place = place;
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> AddPositions(IEnumerable<string> positions)
    {
        var anemic = Clone();
        anemic.Positions = positions.ToList();
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> AddAccessories(
        IEnumerable<string> costumes, IEnumerable<string> toys, IEnumerable<string> fantasies, IEnumerable<string> tags)
    {
        var anemic = Clone();
        anemic.Costumes = costumes.ToList();
        anemic.Toys = toys.ToList();
        anemic.Fantasies = fantasies.ToList();
        anemic.Tags = tags.ToList();
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> RecordContraception(IEnumerable<string> contraceptives, int count)
    {
        var anemic = Clone();
        anemic.Contraceptives = contraceptives.ToList();
        anemic.ContraceptiveCount = count;
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> SpecifyDuration(int durationMin)
    {
        var validator = new DurationPositiveValidator();
        if (!validator.IsSatisfiedBy(durationMin))
            return Fail(validator.Reason);

        var anemic = Clone();
        anemic.DurationMin = durationMin;
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> RecordOrgasms(IEnumerable<IOrgasms> orgasms)
    {
        var list = orgasms.ToList();
        var ratingValidator = new RatingRangeValidator();
        foreach (var orgasm in list)
        {
            if (!ratingValidator.IsSatisfiedBy(orgasm))
                return Fail(ratingValidator.Reason);
        }

        var anemic = Clone();
        anemic.Orgasms = list;
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> Complete()
    {
        var validator = new WhenRequiredValidator();
        if (!validator.IsSatisfiedBy(Model))
            return Fail(validator.Reason);

        var anemic = Clone();
        anemic.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        return Success(anemic);
    }

    public AggregateResult<IJournal, IJournalAnemicModel> Delete()
        => Success(Clone());
}
