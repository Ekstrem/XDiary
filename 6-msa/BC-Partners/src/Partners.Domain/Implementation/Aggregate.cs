namespace Partners.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Specifications;
using XDiary.Contracts;

internal sealed class Aggregate : IPartnerAggregate
{
    public IPartnerAnemicModel Model { get; }
    private Aggregate(IPartnerAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IPartnerAnemicModel model) => new(model);

    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    public IPartnerRoot Root => Model.Root;
    public string Photo => Model.Photo;
    public string Gender => Model.Gender;
    public string Notes => Model.Notes;
    public Guid? ClientId => Model.ClientId;
    public DateTime? DeletedAt => Model.DeletedAt;

    private AggregateResult<IPartners, IPartnerAnemicModel> Success(AnemicModel m)
    {
        var data = BusinessOperationData<IPartners, IPartnerAnemicModel>.Commit<IPartners, IPartnerAnemicModel>(Model, m);
        return new AggregateResultSuccess<IPartners, IPartnerAnemicModel>(data);
    }
    private AggregateResult<IPartners, IPartnerAnemicModel> Fail(string e)
    {
        var data = BusinessOperationData<IPartners, IPartnerAnemicModel>.Commit<IPartners, IPartnerAnemicModel>(Model, Model);
        return new AggregateResultException<IPartners, IPartnerAnemicModel>(data, new FailedSpecification<IPartners, IPartnerAnemicModel>(e));
    }

    public AggregateResult<IPartners, IPartnerAnemicModel> Register(string name, Guid clientId)
    {
        var v = new NameRequiredSpecification();
        var probe = new AnemicModel { Id = Model.Id, Root = PartnerRoot.CreateInstance(name) };
        if (!v.IsSatisfiedBy(probe)) return Fail(v.Reason);
        var m = new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = PartnerRoot.CreateInstance(name), ClientId = clientId };
        return Success(m);
    }
    public AggregateResult<IPartners, IPartnerAnemicModel> Rename(string name)
    {
        var v = new NameRequiredSpecification();
        var probe = new AnemicModel { Root = PartnerRoot.CreateInstance(name) };
        if (!v.IsSatisfiedBy(probe)) return Fail(v.Reason);
        return Success(Clone() with { Root = PartnerRoot.CreateInstance(name) });
    }
    public AggregateResult<IPartners, IPartnerAnemicModel> UpdateProfile(string photo, string gender, string notes)
    {
        var gv = new GenderEnumSpecification();
        var g = gv.Normalize(gender);
        if (!gv.IsSatisfiedBy(g)) return Fail(gv.Reason);
        return Success(Clone() with { Photo = photo, Gender = g, Notes = notes });
    }
    public AggregateResult<IPartners, IPartnerAnemicModel> LinkClient(Guid clientId) => Success(Clone() with { ClientId = clientId });
    public AggregateResult<IPartners, IPartnerAnemicModel> UnlinkClient() => Success(Clone() with { ClientId = null });
    public AggregateResult<IPartners, IPartnerAnemicModel> Remove()
    {
        if (Model.DeletedAt is not null) return Success(Clone()); // РёРґРµРјРїРѕС‚РµРЅС‚РЅРѕ, Р±РµР· РІС‚РѕСЂРѕРіРѕ СЃРѕР±С‹С‚РёСЏ
        return Success(Clone() with { DeletedAt = DateTime.UtcNow });
    }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Photo = Model.Photo, Gender = Model.Gender, Notes = Model.Notes, ClientId = Model.ClientId, DeletedAt = Model.DeletedAt };
}