namespace Catalog.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Specifications;
using XDiary.Contracts;

internal sealed class Aggregate : ICatalogAggregate
{
    public ICatalogAnemicModel Model { get; }
    private Aggregate(ICatalogAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(ICatalogAnemicModel model) => new(model);

    // IAggregate<ICatalog> / IAnemicModel<ICatalog>:
    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    // ICatalogAnemicModel:
    public ICatalogItemRoot Root => Model.Root;
    public string Photo => Model.Photo;
    public string Notes => Model.Notes;
    public string Brand => Model.Brand;
    public bool Archived => Model.Archived;
    public string FantasyPartner => Model.FantasyPartner;
    public Guid? ClientId => Model.ClientId;

    private AggregateResult<ICatalog, ICatalogAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<ICatalog, ICatalogAnemicModel>.Commit<ICatalog, ICatalogAnemicModel>(Model, m); return new AggregateResultSuccess<ICatalog, ICatalogAnemicModel>(d); }
    private AggregateResult<ICatalog, ICatalogAnemicModel> Fail(string e)
    { var d = BusinessOperationData<ICatalog, ICatalogAnemicModel>.Commit<ICatalog, ICatalogAnemicModel>(Model, Model); return new AggregateResultException<ICatalog, ICatalogAnemicModel>(d, new FailedSpecification<ICatalog, ICatalogAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Photo = Model.Photo, Notes = Model.Notes, Brand = Model.Brand, Archived = Model.Archived, FantasyPartner = Model.FantasyPartner, ClientId = Model.ClientId };

    public AggregateResult<ICatalog, ICatalogAnemicModel> AddItem(string kind, string name, Guid clientId)
    {
        var kv = new KindEnumSpecification();
        if (!kv.IsSatisfiedBy(kind)) return Fail(kv.Reason);
        var probe = new AnemicModel { Root = CatalogItemRoot.CreateInstance(kind, name) };
        if (!new NameRequiredSpecification().IsSatisfiedBy(probe)) return Fail("Имя элемента обязательно.");
        var m = new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = CatalogItemRoot.CreateInstance(kind, name), ClientId = clientId };
        return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> Rename(string name)
    {
        var probe = new AnemicModel { Root = CatalogItemRoot.CreateInstance(Model.Root.Kind, name) };
        if (!new NameRequiredSpecification().IsSatisfiedBy(probe)) return Fail("Имя элемента обязательно.");
        var m = Clone() with { Root = CatalogItemRoot.CreateInstance(Model.Root.Kind, name) }; return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> UpdateDetails(string photo, string notes, string brand, string fantasyPartner)
    {
        var m = Clone() with { Photo = photo, Notes = notes, Brand = brand, FantasyPartner = fantasyPartner };
        if (!new BrandToyOnlySpecification().IsSatisfiedBy(m)) return Fail("brand заполняется только для toy.");
        return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> Archive()
    {
        if (!new ArchiveToyOnlySpecification().IsSatisfiedBy(Model)) return Fail("Archive применяется только для toy.");
        var m = Clone() with { Archived = true }; return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> Restore()
    {
        if (!new ArchiveToyOnlySpecification().IsSatisfiedBy(Model)) return Fail("Restore применяется только для toy.");
        var m = Clone() with { Archived = false }; return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> Remove() => Success(Clone());
}
