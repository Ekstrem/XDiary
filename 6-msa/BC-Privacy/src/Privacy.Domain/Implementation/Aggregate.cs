namespace Privacy.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Specifications;
using XDiary.Contracts;

internal sealed class Aggregate : IPinCredentialAggregate
{
    public IPinCredentialAnemicModel Model { get; }
    private Aggregate(IPinCredentialAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IPinCredentialAnemicModel model) => new(model);

    // IAggregate<TBC> / IAnemicModel<TBC>:
    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    // IPrivacyAnemicModel:
    public IPinCredentialRoot Root => Model.Root;
    public bool Locked => Model.Locked;
    private AggregateResult<IPrivacy, IPinCredentialAnemicModel> Success(AnemicModel m, CommandToAggregate? command = null, [System.Runtime.CompilerServices.CallerMemberName] string commandName = "")
    {
        command ??= CommandToAggregate.Commit(m.CorrelationToken, commandName, m.SubjectName, Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Model.Version + 1));
        m = m with { Version = command.Version, CommandName = command.CommandName, SubjectName = command.SubjectName }; // BE-150: Command.Version = новая версия агрегата (timestamp), acted-on Version остаётся в DomainEvent.Version
        var d = BusinessOperationData<IPrivacy, IPinCredentialAnemicModel>.Commit<IPrivacy, IPinCredentialAnemicModel>(Model, m); return new AggregateResultSuccess<IPrivacy, IPinCredentialAnemicModel>(d); }
    private AggregateResult<IPrivacy, IPinCredentialAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IPrivacy, IPinCredentialAnemicModel>.Commit<IPrivacy, IPinCredentialAnemicModel>(Model, Model); return new AggregateResultException<IPrivacy, IPinCredentialAnemicModel>(d, new FailedSpecification<IPrivacy, IPinCredentialAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Locked = Model.Locked };

    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> SetPin(Guid clientId, string salt, string hash, int iterations, CommandToAggregate? command = null)
    {
        if (string.IsNullOrWhiteSpace(hash)) return Fail("Hash обязателен (NoPlaintext).");
        var s = new SinglePinPerClientSpecification();
        if (!s.IsSatisfiedBy(clientId, Model)) return Fail(s.Reason);
        return Success(Clone() with { Root = PinCredentialRoot.CreateInstance(clientId, salt, hash, iterations), Locked = false }, command);
    }
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> ChangePin(string salt, string hash, int iterations, CommandToAggregate? command = null)
    {
        if (string.IsNullOrWhiteSpace(hash)) return Fail("Hash обязателен (NoPlaintext).");
        return Success(Clone() with { Root = PinCredentialRoot.CreateInstance(Model.Root.ClientId, salt, hash, iterations), Locked = false }, command);
    }
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> Remove(CommandToAggregate? command = null)
    { return Success(Clone() with { Root = PinCredentialRoot.CreateInstance(Model.Root.ClientId, "", "", 0), Locked = false }, command); }
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> Lock(CommandToAggregate? command = null)
    { return Success(Clone() with { Locked = true }, command); }
    public AggregateResult<IPrivacy, IPinCredentialAnemicModel> Unlock(CommandToAggregate? command = null)
    { return Success(Clone() with { Locked = false }, command); }
}
