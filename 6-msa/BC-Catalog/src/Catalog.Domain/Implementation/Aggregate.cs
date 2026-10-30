namespace Catalog.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Specifications;
using XDiary.Contracts;

public sealed class Aggregate
{
    public ICatalogAnemicModel Model { get; }
    private Aggregate(ICatalogAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(ICatalogAnemicModel model) => new(model);

    private AggregateResult<ICatalog, ICatalogAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<ICatalog, ICatalogAnemicModel>.Commit<ICatalog, ICatalogAnemicModel>(Model, m); return new AggregateResultSuccess<ICatalog, ICatalogAnemicModel>(d); }
    private AggregateResult<ICatalog, ICatalogAnemicModel> Fail(string e)
    { var d = BusinessOperationData<ICatalog, ICatalogAnemicModel>.Commit<ICatalog, ICatalogAnemicModel>(Model, Model); return new AggregateResultException<ICatalog, ICatalogAnemicModel>(d, new FailedSpecification<ICatalog, ICatalogAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Photo = Model.Photo, Notes = Model.Notes, Brand = Model.Brand, Archived = Model.Archived, FantasyPartner = Model.FantasyPartner };

    public AggregateResult<ICatalog, ICatalogAnemicModel> AddItem(string kind, string name)
    {
        var kv = new KindEnumValidator();
        if (!kv.IsSatisfiedBy(kind)) return Fail(kv.Reason);
        var probe = new AnemicModel { Root = CatalogItemRoot.CreateInstance(kind, name) };
        if (!new NameRequiredValidator().IsSatisfiedBy(probe)) return Fail("Имя элемента обязательно.");
        var m = new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = CatalogItemRoot.CreateInstance(kind, name) };
        return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> Rename(string name)
    {
        var probe = new AnemicModel { Root = CatalogItemRoot.CreateInstance(Model.Root.Kind, name) };
        if (!new NameRequiredValidator().IsSatisfiedBy(probe)) return Fail("Имя элемента обязательно.");
        var m = Clone(); m.Root = CatalogItemRoot.CreateInstance(Model.Root.Kind, name); return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> UpdateDetails(string photo, string notes, string brand, string fantasyPartner)
    {
        var m = Clone(); m.Photo = photo; m.Notes = notes; m.Brand = brand; m.FantasyPartner = fantasyPartner;
        if (!new BrandToyOnlyValidator().IsSatisfiedBy(m)) return Fail("brand заполняется только для toy.");
        return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> Archive()
    {
        if (!new ArchiveToyOnlyValidator().IsSatisfiedBy(Model)) return Fail("Archive применяется только для toy.");
        var m = Clone(); m.Archived = true; return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> Restore()
    {
        if (!new ArchiveToyOnlyValidator().IsSatisfiedBy(Model)) return Fail("Restore применяется только для toy.");
        var m = Clone(); m.Archived = false; return Success(m);
    }
    public AggregateResult<ICatalog, ICatalogAnemicModel> Remove() => Success(Clone());
}