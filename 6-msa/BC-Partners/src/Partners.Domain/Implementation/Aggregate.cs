namespace Partners.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Specifications;
using XDiary.Contracts;

public sealed class Aggregate
{
    public IPartnerAnemicModel Model { get; }
    private Aggregate(IPartnerAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IPartnerAnemicModel model) => new(model);

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

    public AggregateResult<IPartners, IPartnerAnemicModel> Register(string name)
    {
        var v = new NameRequiredValidator();
        var probe = new AnemicModel { Id = Model.Id, Root = PartnerRoot.CreateInstance(name) };
        if (!v.IsSatisfiedBy(probe)) return Fail(v.Reason);
        var m = new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = PartnerRoot.CreateInstance(name) };
        return Success(m);
    }
    public AggregateResult<IPartners, IPartnerAnemicModel> Rename(string name)
    {
        var v = new NameRequiredValidator();
        var probe = new AnemicModel { Root = PartnerRoot.CreateInstance(name) };
        if (!v.IsSatisfiedBy(probe)) return Fail(v.Reason);
        var m = Clone(); m.Root = PartnerRoot.CreateInstance(name); return Success(m);
    }
    public AggregateResult<IPartners, IPartnerAnemicModel> UpdateProfile(string photo, string gender, string notes)
    {
        var gv = new GenderEnumValidator();
        if (!gv.IsSatisfiedBy(gender)) return Fail(gv.Reason);
        var m = Clone(); m.Photo = photo; m.Gender = gender; m.Notes = notes; return Success(m);
    }
    public AggregateResult<IPartners, IPartnerAnemicModel> Remove() => Success(Clone());
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Photo = Model.Photo, Gender = Model.Gender, Notes = Model.Notes };
}