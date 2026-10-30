namespace Privacy.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using XDiary.Contracts;

public sealed class Aggregate
{
    public IPinCredentialAnemicModel Model { get; }
    private Aggregate(IPinCredentialAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IPinCredentialAnemicModel model) => new(model);

    private AggregateResult<IPrivacy, IPinCredentialAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<IPrivacy, IPinCredentialAnemicModel>.Commit<IPrivacy, IPinCredentialAnemicModel>(Model, m); return new AggregateResultSuccess<IPrivacy, IPinCredentialAnemicModel>(d); }
    private AggregateResult<IPrivacy, IPinCredentialAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IPrivacy, IPinCredentialAnemicModel>.Commit<IPrivacy, IPinCredentialAnemicModel>(Model, Model); return new AggregateResultException<IPrivacy, IPinCredentialAnemicModel>(d, new FailedSpecification<IPrivacy, IPinCredentialAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Locked = Model.Locked };

    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> SetPin(string salt, string hash, int iterations)
    {
        if (string.IsNullOrWhiteSpace(hash)) return Fail("Hash обязателен (NoPlaintext).");
        var m = Clone(); m.Root = PinCredentialRoot.CreateInstance(salt, hash, iterations); m.Locked = false; return Success(m);
    }
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> ChangePin(string salt, string hash, int iterations)
    {
        if (string.IsNullOrWhiteSpace(hash)) return Fail("Hash обязателен (NoPlaintext).");
        var m = Clone(); m.Root = PinCredentialRoot.CreateInstance(salt, hash, iterations); m.Locked = false; return Success(m);
    }
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> Remove()
    { var m = Clone(); m.Root = PinCredentialRoot.CreateInstance("", "", 0); m.Locked = false; return Success(m); }
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> Lock()
    { var m = Clone(); m.Locked = true; return Success(m); }
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> Unlock()
    { var m = Clone(); m.Locked = false; return Success(m); }
}